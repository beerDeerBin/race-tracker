import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { config } from './config';
import { tokenStore } from './tokenStore';
import { logger } from './logger';
import type { DeviceStatusUpdate, RunProgressUpdate } from '../models/realtime';

/**
 * The single realtime connection (/F62/, /F63/): one auto-reconnecting WebSocket to the M6
 * hub, with a ref-counted per-vehicle group registry. SignalR does NOT restore server-side
 * group membership after a reconnect, so every registered group is re-subscribed in
 * `onreconnected`. The connection starts lazily with the first subscription and stops when
 * the last one is released (navigating away unsubscribes per /F63/).
 *
 * The token rides the `access_token` query string via `accessTokenFactory` — re-evaluated
 * on every (re)connect, so it always reflects the current session.
 */

type StatusHandler = (update: DeviceStatusUpdate) => void;
type RunProgressHandler = (update: RunProgressUpdate) => void;

export type ConnectionFactory = () => HubConnection;

const defaultConnectionFactory: ConnectionFactory = () =>
    new HubConnectionBuilder()
        .withUrl(`${config.realtimeUrl}/hubs/telemetry`, {
            accessTokenFactory: () => tokenStore.getToken() ?? '',
        })
        .withAutomaticReconnect()
        .build();

export class TelemetryConnection {
    private readonly createConnection: ConnectionFactory;
    private connection: HubConnection | null = null;
    private startPromise: Promise<void> | null = null;
    /** guid → subscriber count; keys are the groups to replay after a reconnect. */
    private readonly groups = new Map<string, number>();
    private readonly statusHandlers = new Set<StatusHandler>();
    private readonly runProgressHandlers = new Set<RunProgressHandler>();

    constructor(createConnection: ConnectionFactory = defaultConnectionFactory) {
        this.createConnection = createConnection;
    }

    /** Registers a status listener; returns the deregistration function. */
    onDeviceStatus(handler: StatusHandler): () => void {
        this.statusHandlers.add(handler);
        return () => this.statusHandlers.delete(handler);
    }

    /** Registers a run-progress listener (6.3 event); returns the deregistration function. */
    onRunProgress(handler: RunProgressHandler): () => void {
        this.runProgressHandlers.add(handler);
        return () => this.runProgressHandlers.delete(handler);
    }

    /**
     * Joins the vehicle's group (ref-counted — multiple components may watch the same
     * device). Returns a release function for unmount/navigation.
     */
    async subscribe(deviceGuid: string): Promise<() => Promise<void>> {
        // Decide the server-group join NOW, before any await: with concurrent subscribes for
        // the same guid (e.g. StrictMode's mount→cleanup→remount) a post-await re-read would
        // see count > 1 in every caller and nobody would ever invoke Subscribe.
        const isFirstSubscriber = (this.groups.get(deviceGuid) ?? 0) === 0;
        this.groups.set(deviceGuid, (this.groups.get(deviceGuid) ?? 0) + 1);

        try {
            await this.ensureStarted();
            if (isFirstSubscriber) {
                await this.connection!.invoke('Subscribe', deviceGuid);
            }
        } catch (error) {
            logger.warn('Realtime subscribe failed', { deviceGuid, error });
        }

        let released = false;
        return async () => {
            if (released) {
                return;
            }
            released = true;
            await this.release(deviceGuid);
        };
    }

    private async release(deviceGuid: string): Promise<void> {
        const count = this.groups.get(deviceGuid) ?? 0;
        if (count <= 1) {
            this.groups.delete(deviceGuid);
        } else {
            this.groups.set(deviceGuid, count - 1);
            return;
        }

        try {
            if (this.connection?.state === HubConnectionState.Connected) {
                await this.connection.invoke('Unsubscribe', deviceGuid);
            }
        } catch (error) {
            logger.warn('Realtime unsubscribe failed', { deviceGuid, error });
        }

        if (this.groups.size === 0) {
            await this.stop();
        }
    }

    private async ensureStarted(): Promise<void> {
        if (this.connection?.state === HubConnectionState.Connected) {
            return;
        }
        if (!this.startPromise) {
            this.connection ??= this.wire(this.createConnection());
            // start() throws on a connection that isn't Disconnected (e.g. Reconnecting):
            // the auto-reconnect is already re-establishing the link and onreconnected
            // replays the registered groups — nothing to do here.
            if (this.connection.state !== HubConnectionState.Disconnected) {
                return;
            }
            this.startPromise = this.connection.start().finally(() => {
                this.startPromise = null;
            });
        }
        await this.startPromise;
    }

    private wire(connection: HubConnection): HubConnection {
        connection.on('DeviceStatus', (update: DeviceStatusUpdate) => {
            for (const handler of this.statusHandlers) {
                handler(update);
            }
        });

        connection.on('RunProgress', (update: RunProgressUpdate) => {
            for (const handler of this.runProgressHandlers) {
                handler(update);
            }
        });

        // SignalR restores the connection but not the server-side groups — replay them.
        connection.onreconnected(() => {
            for (const deviceGuid of this.groups.keys()) {
                connection
                    .invoke('Subscribe', deviceGuid)
                    .catch((error: unknown) =>
                        logger.warn('Group replay after reconnect failed', { deviceGuid, error }),
                    );
            }
        });

        return connection;
    }

    private async stop(): Promise<void> {
        const connection = this.connection;
        this.connection = null;
        this.startPromise = null;
        if (connection) {
            try {
                await connection.stop();
            } catch (error) {
                logger.warn('Realtime connection stop failed', { error });
            }
        }
    }
}

/** App-wide singleton — consumed by hooks only, never by components directly. */
export const telemetryConnection = new TelemetryConnection();
