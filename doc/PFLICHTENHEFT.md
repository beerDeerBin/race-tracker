# Pflichtenheft – race-tracker

> **Zweck dieses Dokuments**
> Dieses Pflichtenheft fasst die Projektidee ([`IDEA.txt`](./IDEA.txt)), den
> aktuellen Stand ([`README.md`](../README.md)) und die Architekturvorgaben
> ([`ARCHITECTURE_PRINCIPLES.md`](./ARCHITECTURE_PRINCIPLES.md)) zu einer
> verbindlichen Anforderungsbasis zusammen. Die funktionalen Anforderungen
> (Abschnitt 5) sind so granular und ID-versehen, dass sich daraus direkt
> **User Stories** ableiten lassen (siehe Abschnitt 12 für das Mapping-Schema).

**Version:** 0.2 · **Stand:** 2026-06-06 · **Status:** Entscheidungen O10–O50 eingearbeitet, bereit für User-Story-Ableitung

---

## 1. Zielbestimmung

Der race-tracker ist ein **Übungsprojekt**, das die im Unterricht erarbeiteten
Prinzipien — **Messaging, Service-Trennung, Persistenz** — an einem greifbaren
Beispiel durchspielt: Ein ferngesteuerter Spielzeug-LKW trägt einen
Mikrocontroller mit Bewegungssensorik. Während der Fahrt erfasst der MC laufend
Lage- und Beschleunigungsdaten und reicht sie über eine entkoppelte
Microservice-Architektur durch, bis sie im Frontend live sichtbar werden.

### 1.1 Muss-Kriterien (verbindlich)

- **/Z10/** Echte Sensordaten eines fahrenden Geräts (ESP32 + IMU) werden über
  MQTT in das System eingespeist.
- **/Z20/** Ein Simulator erzeugt dieselben Datenpakete als alternative,
  hardwareunabhängige Quelle.
- **/Z30/** Eingehende Telemetrie wird entkoppelt über einen Message-Broker
  (RabbitMQ) an die Microservices verteilt.
- **/Z40/** Die Daten werden dauerhaft persistiert und sind jedem Fahrzeug
  eindeutig zugeordnet (GUID).
- **/Z50/** Das Frontend kann neue Fahrzeuge anlegen, Läufe steuern und Daten
  inkl. **Live-Ansicht** anzeigen.

### 1.2 Soll-Kriterien (wünschenswert)

- **/Z60/** Auswertung/Regeln (z. B. Alarm bei kritischer Batterie) mit
  Echtzeit-Benachrichtigung an den Benutzer.
- **/Z70/** Authentifizierung (**Single-User**, /O20/); Fahrzeuge gehören dem Benutzer.
- **/Z80/** Vorberechnete Aggregationen/Downsampling für performante Dashboards.

### 1.3 Kann-Kriterien (optional)

- **/Z90/** Mehrmandantenfähigkeit (Database-per-Tenant).
- **/Z95/** Export von Läufen (CSV/JSON).

### 1.4 Abgrenzungskriterien (ausdrücklich nicht im Umfang)

- **/ZA10/** Keine produktive Skalierung / kein produktiver Betrieb — reiner
  Lern- und Demobetrieb (lokal via Tilt/Docker).
- **/ZA20/** Keine Steuerung der Fahrt des LKW (Lenken/Gas) — nur Erfassung und
  Steuerung der **Messung**.
- **/ZA30/** Keine mobile App; das Frontend ist eine Web-SPA.

---

## 2. Produkteinsatz

| Aspekt | Beschreibung |
|---|---|
| **Anwendungsbereich** | Schulungs-/Übungsprojekt zur Demonstration einer entkoppelten, event-getriebenen Microservice-Architektur an realer Sensorik. |
| **Zielgruppen** | (a) Entwickler/Lernende, die das System bauen und betreiben; (b) Benutzer, die Fahrzeuge anlegen, Läufe starten und Messdaten ansehen. |
| **Betriebsbedingungen** | Lokale Entwicklungsumgebung (Tilt/Docker Compose). MQTT-Broker, RabbitMQ und Services laufen lokal; das reale Gerät verbindet sich per WLAN. |

### 2.1 Akteure

| Akteur | Rolle |
|---|---|
| **Gerät (MCU)** | Reale Datenquelle: ESP32 + LSM6DSOX, sendet Status & Messdaten über MQTT, empfängt Kommandos. |
| **Simulator** | Software-Datenquelle: emuliert ein oder mehrere Geräte protokollgleich. |
| **Benutzer** | Mensch am Frontend: legt Fahrzeuge an, steuert Läufe, sieht Daten/Live-Ansicht. |
| **System** | Die Gesamtheit der Backend-Services (Gateway, Management, Persistenz, Events). |

---

## 3. Produktübersicht

```
 Quellen                          Backend (entkoppelt)                      Client
 ────────                         ──────────────────────                    ──────
 ┌──────────────┐  MQTT (binär)  ┌───────────────┐  RabbitMQ  ┌───────────┐
 │ Gerät (MCU)  │───────────────▶│  Ingestion/   │──────────▶│Persistenz │◀─ GraphQL ─┐
 │  + GUID      │   rt/<guid>/*  │  Gateway      │  (pub/sub) │(Time-Series)│           │
 └──────────────┘                └───────────────┘            └───────────┘            │
 ┌──────────────┐  MQTT          ┌───────────────┐            ┌───────────┐         ┌──┴────────┐
 │ Simulator    │───────────────▶│  (Decode/      │            │ Events/    │── WS ─▶│ Web-SPA   │
 │ (n Geräte)   │                │   Normalisierung)│          │ Rules/     │        │ (Frontend)│
 └──────────────┘                └───────────────┘            │ Notify     │        └──┬────────┘
                                 ┌───────────────┐            └───────────┘    REST/    │
                                 │ Management/    │◀──────── Registry-Lookup ──── GraphQL│
                                 │ Core (CRUD,    │                                      │
                                 │ User/Vehicle,  │◀──────── REST (CRUD, Cmd) ───────────┘
                                 │ Auth, Cmd)     │
                                 └───────────────┘
```

Der **Datenfluss** (aus IDEA.txt):

```
Gerät (MC + GUID) ──▶ MQTT ──▶ Gateway ──▶ RabbitMQ ──▶ Persistenz (speichern)
Simulator         ──▶ MQTT (alternative Quelle)
Frontend ─INIT-VEHICLE─▶ Management ──▶ RabbitMQ (User/Vehicle-Verwaltung & Zuordnung)
RabbitMQ ──▶ Frontend (Rückkanal Live-Ansicht, via WebSocket)
```

> **Hinweis zur Broker-Topologie:** MQTT (Mosquitto) ist der **Geräte-Transport**
> (Telemetrie rein, Kommandos raus). RabbitMQ ist der **interne Service-Broker**
> für die entkoppelte Verteilung zwischen den Microservices. Beide sind bewusst
> getrennt (vgl. Architekturprinzip „Loose Coupling via asynchronem Messaging").

---

## 4. Produktteilprodukte (Komponenten & Status)

| ID | Teilprodukt | Rolle | Status |
|---|---|---|---|
| **TP-MCU** | `race-tracker-mcu` | ESP32-Firmware: IMU-Sampling, MQTT, Statemachine | **vorhanden** |
| **TP-SIM** | `simulator` | Software-Gerät(e), protokollgleich | **vorhanden** |
| **TP-BROKER-MQTT** | `mqtt` (Mosquitto) | MQTT-Broker + MQTTX-Debug-UI | **vorhanden** |
| **TP-GW** | Ingestion/Gateway | MQTT abonnieren, dekodieren, an RabbitMQ republizieren | **zu bauen** |
| **TP-BROKER-AMQP** | RabbitMQ | Interner Service-Broker | **zu bauen** |
| **TP-MGMT** | Management/Core | User, Vehicle, Auth, Registry, Kommando-Versand | **zu bauen** |
| **TP-PERS** | Persistenz/Time-Series | Messdaten speichern + GraphQL-Query | **zu bauen** |
| **TP-EVT** | Events/Rules/Notify | Regeln auswerten, Echtzeit-Push (WS) | **zu bauen** (Soll, **letztes Epic** – Zusatz) |
| **TP-FE** | Web-SPA | Steuerung + Anzeige + Live-Ansicht | **zu bauen** |

> **Reihenfolge (vgl. §13):** Erst der vertikale **Happy-Path-Durchstich**
> (Quelle → TP-GW → TP-BROKER-AMQP → TP-PERS → TP-FE), dann TP-EVT als
> abschließender Zusatz.
>
> **Flask-Tester (`components/mqtt/race-tracker-tester`):** bleibt reines
> **Dev-/Debug-Werkzeug** (neben MQTTX) und wird **nicht weiter ausgebaut**. Er
> gilt als **abgelöst**, sobald TP-FE den Happy-Path-Kern abdeckt
> (/F80/, /F60/, /F61/, /F30–F32/, /F81/) — danach für die Demo nicht mehr nötig,
> aber als Low-Level-Debug-Hilfe weiter nutzbar (siehe /O40/).

---

## 5. Produktfunktionen (Funktionale Anforderungen)

> Legende Priorität: **M** = Muss, **S** = Soll, **K** = Kann.
> Jede Anforderung ist Kandidat für eine oder mehrere User Stories.

### 5.1 Benutzer & Authentifizierung (TP-MGMT)

> **Entscheidung (/O20/): Single-User-Modus.** Keine Self-Registrierung; **ein
> vorab angelegter (seeded) Benutzer** genügt. Token-Login + secure-by-default
> bleiben jedoch erhalten, damit die Auth-Plumbing wie in den Architektur-
> prinzipien demonstriert wird. Das `owner`-Feld bleibt im Datenmodell (/D20/),
> sodass Multi-User später ohne Migration nachrüstbar ist.

| ID | Prio | Anforderung |
|---|---|---|
| **/F10/** | K | Self-Registrierung neuer Benutzer (**out-of-scope für die Übung**; nur falls Multi-User später gewünscht). Falls umgesetzt: Anmeldedaten **gehasht** speichern, nie im Klartext. |
| **/F11/** | M | Der seeded Benutzer kann sich **anmelden** und erhält ein **signiertes Token** (Bearer), das bei jedem geschützten Aufruf geprüft wird. |
| **/F12/** | M | Geschützte Endpunkte sind **secure-by-default**: ohne authentifizierten Principal kein Zugriff (Fallback-Authorization-Policy). |
| **/F13/** | K | Service-zu-Service-Aufrufe authentisieren sich über einen **Service-Key**. |

### 5.2 Fahrzeugverwaltung (TP-MGMT)

| ID | Prio | Anforderung |
|---|---|---|
| **/F20/** | M | Ein **unbekanntes Gerät** wird beim ersten Status automatisch als Fahrzeug im Status **`pending` (unregistriert)** angelegt (GUID vom Gerät übernommen) — siehe /F25/. Der Benutzer kann es anschließend **claimen** (benennen, übernehmen → Status `registered`). Manuelles Anlegen mit frei gewählter GUID bleibt als Alternativweg möglich. |
| **/F21/** | M | Jedes Fahrzeug trägt mindestens: GUID, Anzeigename, Besitzer, **Registrierungsstatus** (`pending`/`registered`), Erstellzeitpunkt. |
| **/F22/** | M | Ein Benutzer kann seine Fahrzeuge **auflisten**, **ansehen**, **bearbeiten** und **löschen** (CRUD). |
| **/F23/** | M | Das Management stellt ein **Registry** bereit, das andere Services abfragen können, um zu erfahren, welche GUIDs bekannt sind und welchem Fahrzeug sie gehören. |
| **/F24/** | M | Eingehende Telemetrie wird anhand der GUID dem korrekten Fahrzeug **zugeordnet**. |
| **/F25/** | M | **Geräte-Discovery (/O50/):** Telemetrie einer **unbekannten GUID** wird **nicht verworfen** — der zuständige Consumer legt **lazy** ein `pending`-Fahrzeug an, damit das Gerät im Dashboard auftaucht und kein Datenverlust entsteht. (Das Gateway bleibt dabei zustandslos und erzwingt selbst **keine** Registry-Prüfung — siehe /F43/.) |

### 5.3 Geräte- & Laufsteuerung (TP-FE → TP-MGMT → Gerät)

> Basis: das bestehende MQTT-Kommandoprotokoll, siehe
> [`PROTOCOL.md`](../components/race-tracker-mcu/PROTOCOL.md) §3–§4.

| ID | Prio | Anforderung |
|---|---|---|
| **/F30/** | M | Ein Benutzer kann ein Gerät **verbinden** (`CONNECT`, IDLE→CONNECTED). |
| **/F31/** | M | Ein Benutzer kann einen **Lauf starten** (`START_RUN`) und dabei **Parameter** wählen: `runId`, `numSamples`, `odr` (12.5–833 Hz), `accelRange` (±2/4/8/16 g), `gyroRange` (±125–2000 dps). |
| **/F32/** | M | Ein Benutzer kann das Gerät **trennen** (`DISCONNECT`) und **zurücksetzen** (`RESET`). |
| **/F33/** | M | Kommandos werden über MQTT an `rt/<guid>/cmd` (QoS 1, optional retained) korrekt **binär kodiert** versendet. |
| **/F34/** | S | Das Frontend zeigt an, ob ein Kommando im aktuellen Gerätezustand **gültig** ist (z. B. `START_RUN` nur in CONNECTED) und verhindert/erklärt ungültige Aktionen. |
| **/F35/** | S | Vor `START_RUN` wird die geräteseitige **Vorab-Validierung** berücksichtigt (Ablehnung bei kritischer Batterie oder nicht geleertem Ringpuffer) und dem Benutzer rückgemeldet (nur über Status sichtbar, kein NACK). |

### 5.4 Telemetrie-Ingestion / Gateway (TP-GW)

| ID | Prio | Anforderung |
|---|---|---|
| **/F40/** | M | Das Gateway **abonniert** die Geräte-Topics über MQTT (`rt/+/status`, `rt/+/data`). |
| **/F41/** | M | Das Gateway **dekodiert** die binären Payloads (Status 24 B; Daten-Batch 24 B Header + n×24 B Samples) hinter einem `IDecoder`-Port und **normalisiert** sie. |
| **/F42/** | M | Normalisierte Nachrichten werden an **RabbitMQ republiziert** (pub/sub) für nachgelagerte Consumer. |
| **/F43/** | M | Das Gateway hält **keine eigene Datenbank** — reine Durchleitung mit Metriken. |
| **/F44/** | M | Bei **Dekodierfehler** wird die Nachricht **verworfen** und eine Metrik erhöht (keine fehlerhaften Daten weiterreichen). |
| **/F45/** | S | Das Gateway ermittelt beim Start über das **Registry** (Retry mit Backoff), welche Geräte zu erwarten sind. |

### 5.5 Persistenz / Time-Series (TP-PERS)

| ID | Prio | Anforderung |
|---|---|---|
| **/F50/** | M | Der Service **konsumiert** normalisierte Messdaten aus RabbitMQ, **validiert** sie und **schreibt** sie in einen **Time-Series-Store**. |
| **/F51/** | M | Schreibpfad (Ingest/Upsert) und Lesepfad sind getrennt (**CQRS-artig**). |
| **/F52/** | M | Eine **GraphQL-Query-API** erlaubt dem Frontend, gezielt Felder/Zeiträume/Aggregationen pro Fahrzeug & Lauf abzufragen. |
| **/F53/** | S | **Roll-ups / Continuous Aggregates** (z. B. nach Zeit-Buckets) liefern vorberechnete, heruntergerechnete Sichten für performante Dashboards. |
| **/F54/** | M | Persistierte Samples behalten die Zuordnung **Fahrzeug (GUID) → Lauf (runId) → Sample-Index**; der Zeitbezug ergibt sich aus ODR (`t = n / odr_hz`, vgl. PROTOCOL §6). |
| **/F55/** | M | Pro Lauf werden Metadaten gespeichert: `runId`, `numSamples`, `odr`, `accelRange`, `gyroRange`, Start-/Endzeit, Anzahl empfangener Samples. |

### 5.6 Live-Ansicht & Echtzeit (TP-EVT/TP-FE)

| ID | Prio | Anforderung |
|---|---|---|
| **/F60/** | M | Das Frontend zeigt den **Gerätestatus live**: Zustand (IDLE/CONNECTED/ACQUIRING), Uptime, Batterie (mV/%), Fehlercodes. |
| **/F61/** | M | Während eines Laufs wird der **Fortschritt live** angezeigt (`sampledCount` / `totalSamples`, ~10 Updates pro Lauf). |
| **/F62/** | M | Push an den Client erfolgt über eine **WebSocket**-Verbindung; Clients **abonnieren eine Ressourcen-Gruppe pro Fahrzeug** (kein Broadcast an alle). |
| **/F63/** | S | Die WebSocket-Verbindung **reconnectet automatisch** und ab-/bestellt Gruppen beim Navigieren. |
| **/F64/** | S | Neu eintreffende Mess-Batches erscheinen ohne Reload in der laufenden Ansicht. |

### 5.7 Events / Regeln / Benachrichtigung (TP-EVT)

| ID | Prio | Anforderung |
|---|---|---|
| **/F70/** | S | Ein **Regelwerk als Daten** (`(Typ, Prädikat, Meldung)`) wird gegen eingehende Status/Messwerte ausgewertet. |
| **/F71/** | S | Mindestens die Regel **„kritische Batterie"** (`PWR_BATTERY_CRITICAL_ERROR`, < 3100 mV) erzeugt ein Ereignis. |
| **/F72/** | S | Ereignisse werden **idempotent** mit **TTL** entprellt (gleiche Bedingung benachrichtigt nur einmal pro Fenster, nicht je Tick). |
| **/F73/** | K | Ausgehende Ereignisse werden über ein **Transactional Outbox** zuverlässig verschickt und per WebSocket an den Benutzer gepusht. |
| **/F74/** | S | Weitere sinnvolle Regeln: „Lauf abgeschlossen", „Gerät offline" (kein Keepalive > N s), „Fehlercode gesetzt". |

### 5.8 Datenvisualisierung (TP-FE)

| ID | Prio | Anforderung |
|---|---|---|
| **/F80/** | M | Das Frontend listet **Fahrzeuge** und je Fahrzeug die **Läufe**. |
| **/F81/** | M | Ein Lauf wird als **Diagramm** der sechs Achsen dargestellt: Beschleunigung `ax/ay/az` (m/s²) und Drehrate `gx/gy/gz` (rad/s) über die Zeit. |
| **/F82/** | S | Der Benutzer kann **Zeitraum/Achsen filtern** und zwischen Roh- und Aggregatansicht wählen. |
| **/F83/** | M | Ein **Dashboard** zeigt alle Geräte mit aktuellem Status auf einen Blick. |
| **/F84/** | K | Läufe können **exportiert** werden (CSV/JSON). |

### 5.9 Simulator (TP-SIM)

| ID | Prio | Anforderung |
|---|---|---|
| **/F90/** | M | Der Simulator erzeugt **protokollgleiche** Pakete (Status & Daten) wie das reale Gerät, sodass das System ohne Hardware testbar ist. |
| **/F91/** | M | Der Simulator kann **mehrere Geräte gleichzeitig** simulieren (konfiguriert über `config.yaml`). |
| **/F92/** | M | Der Simulator **reagiert auf Kommandos** (`CONNECT`/`START_RUN`/`DISCONNECT`/`RESET`) zustandsrichtig wie die Statemachine in PROTOCOL §2. |
| **/F93/** | S | Geräte/Konfiguration sind ohne YAML-Handarbeit über die **Tilt-UI** verwaltbar (Buttons am Simulator-Resource). |

---

## 6. Produktdaten

| ID | Datum | Beschreibung |
|---|---|---|
| **/D10/** | **Benutzer** | id, Anmeldename, Passwort-Hash, Rolle, Erstellzeitpunkt. *(Single-User: ein seeded Datensatz, /O20/.)* |
| **/D20/** | **Fahrzeug** | GUID (UUID), Name, Besitzer (User-Ref), **Registrierungsstatus (`pending`/`registered`)**, Erstellzeitpunkt, Metadaten. |
| **/D30/** | **Gerätestatus** | uptimeMs, batteryMv, batteryPct, status (IDLE/CONNECTED/ACQUIRING), sampledCount, totalSamples, errorCode (64-Bit-Bitmaske). |
| **/D40/** | **Lauf (Run)** | runId (UUID), Fahrzeug-Ref, numSamples, odr, accelRange, gyroRange, Start/Ende, empfangene Samples. |
| **/D50/** | **Sample** | runId, Index, ax/ay/az (m/s²), gx/gy/gz (rad/s); Zeit abgeleitet aus ODR. |
| **/D60/** | **Ereignis/Alarm** | Typ, Fahrzeug-Ref, Auslöser, Meldung, Zeitstempel, Zustellstatus. |

**Speichervielfalt (Polyglot Persistence, je nach Job):** Dokument-DB für
Domänenentitäten (User/Vehicle), Time-Series-DB für Messdaten, Key-Value-Cache
für ephemeren Zustand/Idempotenz, relationale DB für das Outbox.

---

## 7. Produktleistungen (nicht-funktionale Anforderungen)

| ID | Prio | Anforderung |
|---|---|---|
| **/L10/** | M | Das Gateway verarbeitet den Datenstrom eines Laufs (bis 833 Hz, Batches à ≤32 Samples) ohne Datenverlust im Normalbetrieb. |
| **/L20/** | M | Live-Status/Fortschritt erreicht das Frontend **nahe Echtzeit** (Wahrnehmung < ~1 s nach Eintreffen am Gateway). |
| **/L30/** | S | Reihenfolge der Mess-Batches bleibt je Gerät erhalten (Producer liefert geordnet, vgl. PROTOCOL §6). |
| **/L40/** | S | Dashboard-Abfragen nutzen Aggregate, sodass Standard-Sichten **nicht** über Rohdaten rechnen müssen. |
| **/L50/** | M | Jeder Service ist **unabhängig deploybar** und besitzt seinen **eigenen Datenspeicher** (keine geteilte DB). |

---

## 8. Qualitätsanforderungen

| Merkmal | Anforderung |
|---|---|
| **Zuverlässigkeit** | Idempotenz + De-Dup mit TTL; Dead-Letter für „poison messages"; differenzierte Fehlerbehandlung beim Consume (Parse-/Validierungsfehler → reject **ohne** Requeue → Dead-Letter; transienter Fehler → reject **mit** Requeue). „Catch-log-continue" in Schleifen. |
| **Beobachtbarkeit** | Strukturierte Logs mit Kontext (Service, Entity-ID); **Correlation-IDs** über Middleware; **Metriken** (received/processed/failed) scrape-freundlich; **Health-Checks** getrennt in Liveness/Readiness. |
| **Resilienz** | Retry-with-Backoff bei kritischen Startabhängigkeiten; automatische Reconnects zu Broker/Cache; degradierter Lauf wird geloggt und übersprungen, nicht fatal. |
| **Sicherheit** | Token-Auth + Fallback-Authorization-Policy; gehashte Passwörter; Service-Keys intern; globale Exception-Middleware statt Stacktrace-Leaks. |
| **Wartbarkeit** | Pro Service **Clean/Onion-Architektur** (Domain → Application → Infrastructure → Api); Ports in Application, Adapter in Infrastructure; DI je Layer; Options-Pattern für Konfiguration; DTOs getrennt von Entitäten. |
| **Testbarkeit** | Unit-Tests mit gemockten Ports; wenige High-Fidelity-Integrationstests gegen echte Abhängigkeiten in Wegwerf-Containern. |

---

## 9. Benutzungsoberfläche (TP-FE)

| ID | Prio | Anforderung |
|---|---|---|
| **/U10/** | M | Web-SPA mit geschützten Routen (Route-Guards) und **Auth-Context**; nur angemeldete Benutzer sehen Fahrzeuge/Daten. |
| **/U20/** | M | Ansichten: Login, Fahrzeug-Liste/Anlage (inkl. **pending-Geräte claimen**), Geräte-Dashboard, Lauf-Steuerung, Lauf-Detail (Diagramme), Live-Ansicht. |
| **/U30/** | S | Klare Anzeige von Gerätezustand und Fehlercodes (Klartext der Bitmaske, vgl. PROTOCOL §5.1). |
| **/U40/** | S | Alle benutzersichtbaren Texte sind **externalisiert** (i18n-fähig). |
| **/U50/** | S | **Error-Boundaries** fangen Render-Fehler ab; ein zentral konfigurierter HTTP-Client behandelt `401` (Logout) und injiziert das Token. |

> Die SPA spiegelt die Backend-Schichtung: `services → hooks → components`, eine
> einzige konfigurierte HTTP-Instanz mit Interceptoren, getypte Modelle als
> Spiegel der API-Verträge, Echtzeit via auto-reconnectende WebSocket-Abos.

---

## 10. Technische Produktumgebung

### 10.1 Hardware (reales Gerät)

- Adafruit **ESP32 Feather V2**
- Adafruit **LSM6DSOX** IMU (STEMMA QT)
- LiPo **LP-552035** 350 mAh

### 10.2 Software & Rollen (Technologiewahl /O10/)

> Die Rollen stammen aus den Architekturprinzipien (technologieagnostisch, hinter
> Ports). Festgelegte Produkte (an den Übungsstack angelehnt) in Spalte
> **Technologie** — jeweils austauschbar, da hinter einem Port.

| Rolle | Technologie | Einsatz hier |
|---|---|---|
| Backend-Framework | **.NET / ASP.NET Core** | Layered, DI, Hosted Services, Middleware (Template §3) |
| Geräte-Telemetrie-Transport | **MQTT** (Mosquitto) | binär, Topics `rt/<guid>/{cmd,status,data}` |
| Interner Message-Broker | **RabbitMQ** | pub/sub + work-queue, acks, prefetch, dead-lettering |
| Dokument-DB (TP-MGMT) | **MongoDB** | Domänenentitäten (User/Vehicle), Discriminator/Inheritance |
| Time-Series-DB (TP-PERS) | **TimescaleDB** (PostgreSQL) | Messdaten: Hypertables + Continuous Aggregates (Roll-ups) |
| Key-Value-Cache | **Redis** | Idempotenz-Keys / ephemerer Zustand mit TTL |
| Relationale DB (Outbox) | **PostgreSQL** | Backing-Store für das Outbox (darf dieselbe PG-Instanz wie Timescale sein) |
| Query-API | **GraphQL** (HotChocolate) | flexible, getypte Reads |
| Echtzeit-Transport | **WebSocket** (SignalR) | Server→Client-Hub, Gruppe pro Fahrzeug |
| Frontend | **React + TypeScript** | SPA-Schichtung §8 (pages/components/hooks/services, i18n, context) |
| Scheduler | (im .NET-Host) | periodische, nicht-überlappende Jobs (Events/Rules) |
| Orchestrierung (lokal) | **Tilt** / Docker Compose | bringt Broker, DBs und Services hoch |

> Konkrete .NET-Bibliotheken (HotChocolate, SignalR, RabbitMQ-Client/MassTransit,
> Serilog usw.) sind Umsetzungsdetail und können beim Bau festgezurrt werden.

### 10.3 Schnittstellen (verbindlich)

- **/S10/** **MQTT-Geräteprotokoll** exakt nach
  [`PROTOCOL.md`](../components/race-tracker-mcu/PROTOCOL.md): Little-Endian,
  gepackte Structs, kein JSON/Framing; Kommandos (0x01–0x04), Status (24 B),
  Daten-Batch (24 B Header + n×24 B). **Diese Schnittstelle ist gesetzt** und von
  Gateway und Simulator einzuhalten.
- **/S20/** **REST** für CRUD und einfache Lookups (User, Vehicle, Kommando-Versand).
- **/S30/** **GraphQL** für die Messdaten-Reads.
- **/S40/** **WebSocket** für Server→Client-Push (Live-Ansicht, Alarme).
- **/S50/** **RabbitMQ**-Nachrichtenverträge (normalisierte Status-/Mess-Events)
  als getypte Contracts zwischen den Services.

---

## 11. Architektur- & Entwicklungsvorgaben

Verbindlich aus [`ARCHITECTURE_PRINCIPLES.md`](./ARCHITECTURE_PRINCIPLES.md):

- **/A10/** Schnitt nach **Bounded Contexts** — ein deploybarer Service je
  kohärente Verantwortung, jeder mit eigenem Datenspeicher.
- **/A20/** Pro Service **Vier-Schichten-Template** (Domain → Application →
  Infrastructure → Api); Abhängigkeitsrichtung durch Projektreferenzen erzwungen.
- **/A30/** **Ports in Application, Adapter in Infrastructure**, Bindung per
  Layer-DI-Extension; schlanker Entry-Point.
- **/A40/** Konfiguration ausschließlich über das **Options-Pattern** mit
  Section-Konstanten.
- **/A50/** Integration **asynchron wo möglich** (RabbitMQ mit acks +
  Dead-Letter), **synchron wo nötig** (REST/GraphQL über getypte Clients).
- **/A60/** Zuverlässigkeitsmuster wo Seiteneffekte zählen: **Transactional
  Outbox**, **TTL-Idempotenz**, Retries + graceful-skip.
- **/A70/** **Generics** für CRUD-Controller/-Services/-Repositories; **Unit of
  Work** für atomare Writes.
- **/A80/** Von Tag 1 **betreibbar**: strukturierte Logs, Correlation-IDs,
  Metriken, Liveness/Readiness.
- **/A90/** **Secure-by-default** und Frontend-Schichtung wie in Abschnitt 8/9.

---

## 12. Ableitung von User Stories (Mapping-Schema)

Aus diesem Pflichtenheft werden User Stories wie folgt gezogen:

> **Als** `<Akteur aus §2.1>` **möchte ich** `<Funktion aus §5 (/Fxx/)>`,
> **damit** `<Nutzen, abgeleitet aus dem Ziel §1>`.

**Akzeptanzkriterien** je Story speisen sich aus:
- der konkreten Anforderung (/Fxx/) und ihren Datenfeldern (§6),
- den nicht-funktionalen Schranken (§7) und Qualitätsmerkmalen (§8),
- der gesetzten Schnittstelle (§10.3, insbesondere PROTOCOL.md).

**Beispiele:**

| Story | Quelle | Skizze |
|---|---|---|
| Gerät claimen | /F20/, /F25/, /D20/ | „Als Benutzer möchte ich ein automatisch entdecktes `pending`-Gerät benennen und übernehmen, damit seine Daten mir zugeordnet sind." AK: unbekannte GUID erscheint als `pending` im Dashboard; nach Claim Status `registered`, Name gesetzt, Besitzer = ich. |
| Lauf starten | /F31/, /S10/, /D40/ | „Als Benutzer möchte ich einen Lauf mit ODR/Range starten, damit das Gerät misst." AK: korrektes 25-Byte-`START_RUN` an `rt/<guid>/cmd`; Status wechselt zu ACQUIRING. |
| Live-Fortschritt sehen | /F61/, /F62/, /L20/ | „Als Benutzer möchte ich den Lauf-Fortschritt live sehen, damit ich den Verlauf verfolge." AK: `sampledCount/totalSamples` aktualisiert sich per WebSocket ohne Reload. |
| Messdaten visualisieren | /F81/, /F52/, /D50/ | „Als Benutzer möchte ich die sechs Achsen eines Laufs als Diagramm sehen." AK: ax/ay/az & gx/gy/gz über Zeit; Zeitbezug aus ODR. |
| Batterie-Alarm | /F71/, /F72/, /D60/ | „Als Benutzer möchte ich bei kritischer Batterie benachrichtigt werden." AK: genau eine Benachrichtigung pro Fenster; Auslöser dokumentiert. |

---

## 13. Getroffene Entscheidungen

| ID | Thema | Entscheidung |
|---|---|---|
| **/O10/** | Technologiewahl | **.NET** (Backend), **MongoDB** (Management), **TimescaleDB/PostgreSQL** (Persistenz + Outbox), **RabbitMQ** (Broker), **Redis** (Cache), **GraphQL/HotChocolate**, **SignalR** (WS), **React+TS** (FE). Details in §10.2. |
| **/O20/** | Auth-Umfang | **Single-User**: ein seeded Benutzer, Token-Login + secure-by-default bleiben; keine Self-Registrierung. `owner`-Feld bleibt → Multi-User später nachrüstbar. Siehe §5.1. |
| **/O30/** | Events/Rules | **Enthalten, aber als letztes Epic** (Zusatz). Erst der Happy-Path-Durchstich Quelle→Persistenz→Anzeige. Siehe §4. |
| **/O40/** | Flask-Tester | **Parallel bauen, nicht wegwerfen.** Bleibt Dev-/Debug-Werkzeug, wird nicht ausgebaut; **abgelöst**, sobald TP-FE den Happy-Path-Kern abdeckt (/F80/, /F60/, /F61/, /F30–F32/, /F81/). Siehe §4. |
| **/O50/** | Unbekannte GUIDs | **Auto-Pending statt verwerfen.** Gateway zustandslos (kein Registry-Enforcement); Consumer legt unbekannte GUID lazy als `pending`-Fahrzeug an; Benutzer claimed es später. Siehe /F25/. |

### 13.1 Noch zu klären beim Bau (Detailfragen, keine Blocker für Stories)

- **/O60/** Genauer Zuständige für die `pending`-Anlage (/F25/): Management-Consumer
  vs. Persistenz-Consumer — abhängig davon, wer als Erster auf einer unbekannten
  GUID aufsetzt.
- **/O70/** Schwellwert „Gerät offline" (/F74/): nach wie vielen ausbleibenden
  Keepalives (~5 s-Takt) gilt ein Gerät als offline.
- **/O80/** Konkrete .NET-Bibliotheken (z. B. RabbitMQ-Client vs. MassTransit) und
  ob Outbox & Timescale dieselbe PostgreSQL-Instanz teilen.

---

## 14. Glossar

| Begriff | Bedeutung |
|---|---|
| **GUID/UUID** | Eindeutige, feste Gerätekennung; im EEPROM gespeichert, als UUID-String dargestellt. |
| **ODR** | Output Data Rate des IMU (12.5–833 Hz); definiert den Zeitbezug der Samples. |
| **Lauf (Run)** | Eine Mess-Session mit fester Sample-Zahl und Konfiguration; identifiziert durch `runId`. |
| **Batch** | MQTT-Nachricht mit bis zu 32 Samples (24-B-Header + n×24-B-Records). |
| **Gateway/Ingestion** | Zustandsloser Edge-Service: dekodiert MQTT, republiziert an RabbitMQ. |
| **Registry** | Vom Management bereitgestelltes Verzeichnis gültiger Geräte/Fahrzeuge. |
| **Outbox** | Transaktionales Muster zur garantierten Ereigniszustellung. |
| **Ports & Adapters** | Hexagonale Architektur: Interfaces (Ports) im Kern, Implementierungen (Adapter) außen. |
