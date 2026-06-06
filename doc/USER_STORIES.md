# User Stories – race-tracker

> Abgeleitet aus dem [Pflichtenheft](./PFLICHTENHEFT.md). Jede Story verweist auf
> die zugrunde liegenden Anforderungen (`/Fxx/`, `/Dxx/`, `/Sxx/`, `/Lxx/`,
> `/Axx/`) dort.

**Version:** 1.0 · **Stand:** 2026-06-06

---

## Wie dieses Dokument zu lesen ist

- **Nummerierung = Bearbeitungsreihenfolge.** Strikt von **1.1 bis 8.x** von oben
  nach unten abarbeiten. Eine Story hängt **nur von kleiner nummerierten** ab —
  nie von einer späteren. (Konkret: 3.x braucht nie, dass 5.x fertig ist.)
- **Priorität:** **M** = Muss (Happy-Path), **S** = Soll, **K** = Kann.
- **Format je Story:** *Als … möchte ich … damit …* + **AK** (Akzeptanz­kriterien,
  möglichst gegen echte Infra prüfbar).

### Leitprinzipien (gelten für **jede** Story)

1. **Anti-Stub / Anti-Mock.** Es wird der **echte Adapter** gebaut (kein
   In-Memory-Repo, das später ersetzt wird; kein Fake-Backend hinter der UI).
   Mocks ausschließlich in **Unit-Tests**; Integrationstests gegen **echte
   Abhängigkeiten in Test-Containern** (vgl. Pflichtenheft §9 Testbarkeit).
2. **Anti-Refactor – einmalig festgezurrt:**
   - **GUID (UUID-String) ist der service-übergreifende Korrelationsschlüssel.**
     Telemetrie (Timescale) ist nach `guid` + `runId` + Sample-Index geschlüsselt,
     Fahrzeuge (MongoDB) nach `guid`. **Keine service-übergreifenden FKs, keine
     geteilte DB** → kein späteres Re-Keying.
   - **Verträge an ihrer Grenze einmal definieren:** RabbitMQ-Nachrichten­verträge
     (`/S50/`) entstehen in M2, API-DTOs in M4/M5 — danach nicht umgebaut.
   - **Frontend erst, wenn seine echten Endpunkte existieren** (→ M7), nie gegen
     Mock-Backends.
   - **Realtime/Notification-Service** wird in **M6** angelegt und in **M8 nur
     erweitert** (kein zweiter Push-Pfad).
   - **Generische CRUD-Basisklassen** entstehen in **M5** (erste CRUD-Entität) und
     werden wiederverwendet — nicht nachträglich extrahiert.
3. **Jeder Service nach dem 4-Schichten-Template** (Domain → Application →
   Infrastructure → Api), Ports in Application, Adapter in Infrastructure,
   Observability (strukturierte Logs, Correlation-ID, Health live/ready) ab der
   Grundgerüst-Story (`/A20–A40/`, `/A80/`).

### Verifikations-Werkzeuge (ohne Frontend, alles real)

`mosquitto_pub`/`mosquitto_sub` · MQTTX · RabbitMQ-Management-UI · `psql` /
TimescaleDB · GraphQL-Playground (Banana Cake Pop) · `curl`/Bruno · ein
SignalR-Testclient · **der bereits fertige Simulator** als echte Datenquelle.

### Bestehender Stand (Voraussetzung, schon fertig)

TP-MCU (Firmware), TP-SIM (Simulator), TP-BROKER-MQTT (Mosquitto) laufen und
publizieren **echte** binäre MQTT-Nachrichten. Die Pipeline wird **flussabwärts**
ab der echten Quelle gebaut.

---

## M1 — Fundament & Infrastruktur

**Ziel:** Die Basis, auf der alle Services aufsetzen, steht real (Broker, Repo-
Konventionen). **Voraussetzung:** bestehender Stand.

### 1.1 — RabbitMQ in den Stack aufnehmen · **M**
*Als Entwickler möchte ich RabbitMQ über Tilt/Compose mit hochfahren, damit die
Services einen echten internen Broker zum Anbinden haben.*
**Verweise:** `/Z30/`, /TP-BROKER-AMQP/, §10.2
**AK:**
- `tilt up` bringt Mosquitto, Simulator **und** RabbitMQ gemeinsam hoch.
- RabbitMQ-Management-UI erreichbar (`:15672`), Broker auf `:5672`.
- Vhost/Exchanges-Konvention dokumentiert (Topic-Exchange für Status & Daten).

### 1.2 — Repo-/Solution-Konventionen festlegen · **M**
*Als Entwickler möchte ich die Solution-Struktur, Namens- und Layer-Konventionen
einmal festlegen, damit kein späteres Umstrukturieren nötig wird.*
**Verweise:** `/A10/`, `/A20/`, `/A40/`
**AK:**
- Dokumentierte Ordner-/Projekt-Konvention: ein Service = 4 Projekte
  (`*.Domain/.Application/.Infrastructure/.Api`), je eigene Solution oder klar
  getrennt im Monorepo.
- `.editorconfig` + zentrale Paketverwaltung (Directory.Packages.props) vorhanden.
- Konvention für Options-Pattern (Section-Konstanten) und DI-Extension je Layer
  beschrieben.

> 📌 **Offen (später, /O80/):** konkrete .NET-Bibliothekswahl
> (RabbitMQ-Client vs. MassTransit) — wird in M2 beim ersten echten Consumer/
> Producer entschieden. *Lassen wir vorerst außen vor.*

---

## M2 — Ingestion / Gateway (TP-GW)

**Ziel:** Echte binäre MQTT-Telemetrie wird dekodiert, normalisiert und an
RabbitMQ republiziert. **Voraussetzung:** M1.

### 2.1 — Gateway-Service-Grundgerüst · **M**
*Als Entwickler möchte ich das Gateway als lauffähigen 4-Schichten-Service mit
Observability aufsetzen, damit alle weiteren Gateway-Funktionen darauf aufsetzen.*
**Verweise:** `/A20/`, `/A30/`, `/A40/`, `/A80/`
**AK:**
- Service startet in Tilt; `/health/live` und `/health/ready` antworten
  (Readiness prüft MQTT- + RabbitMQ-Erreichbarkeit).
- Strukturierte Logs + Correlation-ID-Middleware aktiv.
- **Building-Blocks** (Serilog-Setup, Health-Helper, Correlation-ID) als
  wiederverwendbares Projekt angelegt — wird ab M3 weiterverwendet (kein späteres
  Extrahieren).

> 📌 **Entscheidung hier (/O80/):** RabbitMQ-Bibliothek festlegen und konsistent
> in allen folgenden Services verwenden.

### 2.2 — MQTT abonnieren & binär dekodieren · **M**
*Als System möchte ich Status- und Daten-Topics abonnieren und die gepackten
Structs hinter einem `IDecoder`-Port dekodieren, damit aus rohen Bytes typisierte
Messwerte werden.*
**Verweise:** `/F40/`, `/F41/`, `/F44/`, `/S10/`
**AK:**
- Abo auf `rt/+/status` und `rt/+/data`; mit laufendem Simulator erscheinen
  dekodierte Status (24 B) und Daten-Batches (24 B Header + n×24 B) in Logs/Metrik.
- Little-Endian/Packing exakt nach [PROTOCOL.md](../components/race-tracker-mcu/PROTOCOL.md).
- Fehlerhafte Payloads werden **verworfen + Metrik erhöht** (nicht weitergereicht).

### 2.3 — Normalisieren & an RabbitMQ republizieren · **M**
*Als System möchte ich dekodierte Nachrichten als typisierte Verträge an RabbitMQ
veröffentlichen, damit nachgelagerte Services entkoppelt konsumieren können.*
**Verweise:** `/F42/`, `/F43/`, `/S50/`, `/L30/`
**AK:**
- Nachrichtenverträge (Status-Event, Sample-Batch-Event) **einmal definiert**
  (inkl. `guid`, `runId`, Felder) und dokumentiert.
- Veröffentlichte Nachrichten in einer gebundenen Test-Queue / im RabbitMQ-UI
  sichtbar und feld-korrekt.
- Gateway hält **keine DB**; Reihenfolge je Gerät bleibt erhalten.

---

## M3 — Persistenz: Schreibpfad (TP-PERS)

**Ziel:** Normalisierte Telemetrie landet validiert in TimescaleDB.
**Voraussetzung:** M2 (echte RabbitMQ-Nachrichten fließen).

### 3.1 — TimescaleDB + Schema/Migrations · **M**
*Als Entwickler möchte ich TimescaleDB mit Hypertable und Run-Metadaten-Schema
hochfahren, damit Messdaten zeitreihen-effizient gespeichert werden können.*
**Verweise:** `/F54/`, `/F55/`, §10.2
**AK:**
- Timescale läuft in Tilt; Migrations legen **Hypertable** für Samples
  (`guid, runId, sampleIndex, ax,ay,az, gx,gy,gz`) + Tabelle für Run-Metadaten an.
- Zeitbezug-Konvention dokumentiert: `t = sampleIndex / odr_hz` (kein Timestamp im
  Sample).

> 📌 **Offen (später, /O80/):** ob das Outbox (M8) **dieselbe** PostgreSQL-Instanz
> nutzt wie Timescale. *Vorerst außen vor — hier nur Timescale.*

### 3.2 — Persistenz-Service-Grundgerüst · **M**
*Als Entwickler möchte ich den Persistenz-Service als 4-Schichten-Service mit
Observability aufsetzen (Building-Blocks aus M2 wiederverwenden), damit Schreib-
und Lesepfad darauf aufsetzen.*
**Verweise:** `/A20/`, `/A80/`, `/L50/`
**AK:** Service startet, Health live/ready (Readiness prüft RabbitMQ + Timescale),
strukturierte Logs; Building-Blocks referenziert (nicht kopiert).

### 3.3 — RabbitMQ-Consumer → Upsert in Timescale · **M**
*Als System möchte ich normalisierte Status/Daten konsumieren, validieren und in
Timescale upserten, damit ein kompletter Lauf dauerhaft gespeichert ist.*
**Verweise:** `/F50/`, `/F51/` (Write-Seite), `/F54/`, `/F55/`, `/L10/`, `/A50/`
**AK:**
- Voller Simulator-Lauf (z. B. 8330 Samples) erzeugt in Timescale die korrekte
  Sample-Anzahl (per `psql` prüfbar) + einen Run-Metadatensatz.
- **Manual-Ack + Prefetch**; differenzierte Fehlerbehandlung: Parse-/
  Validierungsfehler → reject **ohne** Requeue → Dead-Letter; transient → reject
  **mit** Requeue.
- Idempotenter Upsert (gleicher `guid/runId/index` doppelt → keine Duplikate).

---

## M4 — Persistenz: Lesepfad / GraphQL (TP-PERS)

**Ziel:** Gespeicherte Daten sind über GraphQL flexibel abfragbar.
**Voraussetzung:** M3 (echte Daten liegen in Timescale).

### 4.1 — GraphQL-Query-API (Read) · **M**
*Als Client möchte ich Samples und Läufe gezielt per GraphQL abfragen (Fahrzeug,
Lauf, Zeitraum, Felder), damit ich genau die Daten bekomme, die ich brauche.*
**Verweise:** `/F51/` (Read-Seite), `/F52/`, `/S30/`
**AK:**
- GraphQL-Playground liefert echte gespeicherte Daten: Läufe je `guid`, Samples je
  `runId` mit Feld-/Zeitraum-Auswahl.
- Read-Pfad ist vom Write-Pfad getrennt (CQRS-artig).

### 4.2 — Roll-ups / Continuous Aggregates · **S**
*Als Client möchte ich vorberechnete, heruntergerechnete Sichten abfragen, damit
Dashboards nicht über Rohdaten rechnen.*
**Verweise:** `/F53/`, `/L40/`
**AK:** Time-Bucket-Aggregate (z. B. Min/Max/Avg je Achse pro Bucket) als
Continuous Aggregate; eigene GraphQL-Query liefert die Aggregatansicht.

---

## M5 — Management & Steuerung (TP-MGMT)

**Ziel:** Identität (Single-User), Fahrzeuge/Registry, Geräte-Discovery und
**Kommando-Versand** ans echte Gerät. **Voraussetzung:** M2 (Status-Strom für
Discovery), MQTT-Quelle (für Kommandos). *(Unabhängig von M3/M4 — steht hier wegen
der Gesamtreihenfolge.)*

### 5.1 — MongoDB + Management-Service-Grundgerüst · **M**
*Als Entwickler möchte ich den Management-Service (4 Schichten) mit MongoDB-
Anbindung aufsetzen, damit User/Vehicle darauf aufbauen.*
**Verweise:** `/A20/`, `/A80/`, §10.2
**AK:** MongoDB läuft in Tilt; Service startet, Health live/ready (Readiness prüft
Mongo); Building-Blocks wiederverwendet.

### 5.2 — Single-User-Auth (Login + secure-by-default) · **M**
*Als Benutzer möchte ich mich mit dem seeded Konto anmelden und ein signiertes
Token erhalten, damit geschützte Endpunkte nur mir offenstehen.*
**Verweise:** `/F11/`, `/F12/`, `/D10/`, §8 Sicherheit
**AK:**
- Seeded Benutzer mit **gehashtem** Passwort; `POST /login` liefert Bearer-Token.
- Geschützter Endpunkt: ohne Token **401**, mit gültigem Token **200**
  (Fallback-Authorization-Policy, secure-by-default).

### 5.3 — Fahrzeug-CRUD + generische Basisklassen + Registry · **M**
*Als Benutzer möchte ich meine Fahrzeuge anlegen/auflisten/bearbeiten/löschen,
damit ich sie verwalten kann; intern soll das über generische CRUD-Bausteine
laufen.*
**Verweise:** `/F21/`, `/F22/`, `/F23/`, `/D20/`, `/A70/`
**AK:**
- `Controller<T>` / `CrudService<T>` / `Repository<T>` + Unit of Work **einmal**
  als Generik; Vehicle bindet sie.
- REST-CRUD für Vehicle (mit `guid`, Name, Besitzer, Registrierungsstatus).
- **Registry**-Endpoint liefert bekannte GUIDs → zugehöriges Fahrzeug.

### 5.4 — Geräte-Discovery (pending) + Claim · **M**
*Als Benutzer möchte ich, dass ein unbekanntes Gerät automatisch als „pending"
auftaucht und ich es benennen/übernehmen kann, damit keine Daten verloren gehen
und ich Geräte nicht blind abtippen muss.*
**Verweise:** `/F20/`, `/F24/`, `/F25/`, `/D20/`
**AK:**
- Management konsumiert Status-Events; **unbekannte GUID → lazy `pending`-Fahrzeug**.
- Simulator mit neuer GUID starten → `pending`-Fahrzeug per REST sichtbar.
- `Claim`-Endpoint setzt Name + Besitzer, Status → `registered`.

> 📌 **Offen (später, /O60/):** ob die `pending`-Anlage im Management- **oder** im
> Persistenz-Consumer sitzt (wer zuerst auf einer unbekannten GUID aufsetzt).
> Aktuell: Management. *Lassen wir als Notiz stehen.*

### 5.5 — Kommando-Versand (REST → MQTT) · **M**
*Als Benutzer möchte ich ein Gerät verbinden, einen Lauf mit Parametern starten,
trennen und zurücksetzen, damit ich Messungen steuere.*
**Verweise:** `/F30/`, `/F31/`, `/F32/`, `/F33/`, `/F35/`, `/S10/`, `/D40/`
**AK:**
- REST-Endpunkte kodieren die **exakten** Binärkommandos (`CONNECT 0x01`,
  `START_RUN 0x02` inkl. `runId/numSamples/odr/accelRange/gyroRange`,
  `DISCONNECT 0x03`, `RESET 0x04`) und publizieren auf `rt/<guid>/cmd` (QoS 1,
  retained).
- **End-to-End real:** `START_RUN` aus diesem Service → Gerät/Simulator geht
  ACQUIRING → Lauf endet → Daten liegen über die M2/M3-Pipeline in Timescale.
- Geräteseitige Vorab-Validierung berücksichtigt (`/F35/`): Ablehnung ist nur über
  den Status sichtbar (kein NACK).

---

## M6 — Echtzeit-Push (Realtime/Notification-Service, Teil 1)

**Ziel:** Live-Status & Fortschritt werden per WebSocket an interessierte Clients
gepusht. **Voraussetzung:** M2 (Status-Events). *Dieser Service wird in **M8 nur
erweitert** (Regeln/Outbox) — keine Neuanlage.*

### 6.1 — Realtime-Service-Grundgerüst + SignalR-Hub · **M**
*Als Entwickler möchte ich den Realtime/Notification-Service mit einem SignalR-Hub
aufsetzen, der Gruppen **pro Fahrzeug (guid)** führt, damit Push gezielt erfolgt.*
**Verweise:** `/F62/`, `/S40/`, `/A80/`
**AK:** Service startet (Health live/ready); SignalR-Hub-Endpoint erreichbar;
Client kann eine `guid`-Gruppe abonnieren/abbestellen.

### 6.2 — Live-Status-Relay · **M**
*Als Benutzer möchte ich den Gerätestatus live sehen, damit ich Zustand, Uptime,
Batterie und Fehlercodes in Echtzeit verfolge.*
**Verweise:** `/F60/`, `/L20/`, `/D30/`
**AK:** Service konsumiert Status-Events aus RabbitMQ und pusht an die `guid`-
Gruppe; ein SignalR-Testclient empfängt live, während der Simulator publiziert.

### 6.3 — Fortschritts-Push während eines Laufs · **M**
*Als Benutzer möchte ich den Lauf-Fortschritt live sehen, damit ich den Verlauf
mitbekomme.*
**Verweise:** `/F61/`
**AK:** Während ACQUIRING steigen `sampledCount`/`totalSamples` beim Testclient
(~10 Updates pro Lauf), ohne Polling.

---

## M7 — Frontend (TP-FE)

**Ziel:** Web-SPA gegen die **realen** Endpunkte (REST M5, GraphQL M4, SignalR
M6). **Voraussetzung:** M4, M5, M6. *(Bewusst spät — nie gegen Mock-Backends.)*
Der Flask-Tester gilt ab Abschluss von 7.5 als abgelöst (`/O40/`).

### 7.1 — App-Grundgerüst + Auth · **M**
*Als Benutzer möchte ich mich einloggen und nur dann geschützte Bereiche sehen,
damit der Zugang gesichert ist.*
**Verweise:** `/U10/`, `/U50/`, `/F11/`, §8 Frontend
**AK:** Geschichteter Aufbau (`services → hooks → components`); **eine** HTTP-
Instanz mit Interceptoren (Token-Injektion, zentrales `401`→Logout); Auth-Context +
Route-Guards; Login gegen echtes Management.

### 7.2 — Geräte-Dashboard + Live-Status · **M**
*Als Benutzer möchte ich alle Geräte mit aktuellem (Live-)Status auf einen Blick
sehen, damit ich den Überblick habe.*
**Verweise:** `/F83/`, `/F60/`, `/F62/`, `/F63/`
**AK:** Fahrzeugliste (REST) inkl. `pending`-Geräte; Live-Status über SignalR;
WebSocket **reconnectet automatisch** und ab-/bestellt Gruppen beim Navigieren.

### 7.3 — Gerät claimen · **M**
*Als Benutzer möchte ich ein entdecktes `pending`-Gerät benennen und übernehmen,
damit seine Daten mir zugeordnet sind.*
**Verweise:** `/F20/`, `/F25/`
**AK:** Claim aus der UI; Status wechselt `pending → registered`, Name + Besitzer
gesetzt.

### 7.4 — Laufsteuerung · **M**
*Als Benutzer möchte ich Läufe per UI verbinden/starten/trennen/zurücksetzen,
damit ich Messungen steuere.*
**Verweise:** `/F30/`, `/F31/`, `/F32/`, `/F34/`
**AK:** Steuer-Buttons rufen Management-REST; im aktuellen Zustand **ungültige**
Aktionen sind deaktiviert/erklärt (`/F34/`); ein aus der UI gestarteter Lauf zeigt
**Live-Fortschritt** (nutzt 6.3).

### 7.5 — Lauf-Liste + Lauf-Detail-Diagramme · **M**
*Als Benutzer möchte ich je Fahrzeug die Läufe sehen und einen Lauf als Diagramm
der sechs Achsen, damit ich die Messung auswerte.*
**Verweise:** `/F80/`, `/F81/`, `/F52/`, `/D50/`
**AK:** Lauf-Liste je Fahrzeug; Diagramm `ax/ay/az` (m/s²) + `gx/gy/gz` (rad/s)
über die Zeit (Zeit aus ODR), Daten via GraphQL.

> ✅ Nach 7.5 ist der Happy-Path im Frontend vollständig → **Flask-Tester
> abgelöst** (`/O40/`), bleibt nur noch Debug-Hilfe.

### 7.6 — Filter + Aggregatansicht · **S**
*Als Benutzer möchte ich Zeitraum/Achsen filtern und zwischen Roh- und
Aggregatansicht wählen, damit große Läufe übersichtlich bleiben.*
**Verweise:** `/F82/`, `/F53/`
**AK:** Filter wirken auf das Diagramm; Umschalten Roh/Aggregat (nutzt 4.2).

### 7.7 — Live-Mess-Ansicht · **S**
*Als Benutzer möchte ich neu eintreffende Mess-Batches ohne Reload sehen, damit ich
den Lauf live mitverfolge.*
**Verweise:** `/F64/`
**AK:** Während/nach einem Lauf erscheinen neue Batches ohne Seiten-Reload.

### 7.8 — Fehlercode-Klartext + i18n · **S**
*Als Benutzer möchte ich Fehlercodes im Klartext und Texte in meiner Sprache,
damit die Oberfläche verständlich ist.*
**Verweise:** `/U30/`, `/U40/`
**AK:** errorCode-Bitmaske → benannte Klartexte (PROTOCOL §5.1); alle Strings
externalisiert (i18n).

---

## M8 — Events / Rules / Notifications (TP-EVT, Teil 2)

**Ziel:** Regelauswertung + zuverlässige Benachrichtigung — als **Erweiterung des
M6-Services**. **Voraussetzung:** M6 (Service + SignalR), M2 (Status-Strom).
*Letztes Epic / Zusatz (`/O30/`).*

### 8.1 — Regel-Engine als Daten · **S**
*Als System möchte ich Regeln als deklarative `(Typ, Prädikat, Meldung)`-Tabelle
gegen eingehende Status/Messwerte auswerten, damit neue Regeln eine Datenänderung
sind.*
**Verweise:** `/F70/`
**AK:** Regelsatz als Daten; Auswerteschleife erzeugt Ereignisse (zunächst in Log/
Metrik sichtbar); Hinzufügen einer Regel ändert nur die Datentabelle.

### 8.2 — Regel „kritische Batterie" + TTL-Idempotenz · **S**
*Als Benutzer möchte ich bei kritischer Batterie benachrichtigt werden, aber nicht
wiederholt, damit die Warnung nützlich bleibt.*
**Verweise:** `/F71/`, `/F72/`, `/D60/`
**AK:** Bedingung (Batterie < 3100 mV bzw. errorCode-Bit 42) → **genau eine**
Benachrichtigung pro Zeitfenster (Cache-Key + TTL).

### 8.3 — Transactional Outbox + Dispatch über SignalR · **K**
*Als Benutzer möchte ich Benachrichtigungen zuverlässig erhalten, auch wenn der
Push-Empfänger kurz weg war, damit nichts verloren geht.*
**Verweise:** `/F73/`, `/A60/`
**AK:** Ereignis wird in **derselben Transaktion** in eine Outbox-Tabelle
(PostgreSQL) geschrieben; Background-Dispatcher pusht per SignalR; Zustellung
übersteht einen Service-Neustart (genau-einmal-Wirkung).

### 8.4 — Weitere Regeln (Lauf fertig / offline / Fehlercode) · **S**
*Als Benutzer möchte ich auch über „Lauf abgeschlossen", „Gerät offline" und
„Fehlercode gesetzt" informiert werden, damit ich den Betrieb im Blick habe.*
**Verweise:** `/F74/`
**AK:** Die drei Regeln erzeugen Ereignisse über denselben TTL-/Push-Pfad.

> 📌 **Offen (später, /O70/):** Schwellwert „Gerät offline" — nach wie vielen
> ausbleibenden Keepalives (~5 s-Takt) gilt ein Gerät als offline. *Vorerst außen
> vor; beim Bau von 8.4 festlegen.*

---

## Abhängigkeits-Überblick (nur rückwärts)

```
M1 Fundament
   └─▶ M2 Gateway ──┬─▶ M3 Persistenz(write) ─▶ M4 GraphQL ──┐
                    │                                         │
                    ├─▶ M5 Management/Steuerung ──────────────┤
                    │                                         ▼
                    ├─▶ M6 Realtime/SignalR ──────────────▶ M7 Frontend
                    │            │
                    └────────────┴─▶ M8 Events/Rules  (Endknoten, Zusatz)
```

Jeder Pfeil zeigt von einer **früheren auf eine spätere** Story (Voraussetzung →
Nutzer). Nichts hängt von M8 ab; M7 hängt nur von M4/M5/M6 ab. Es gibt **keine**
Rückwärts-/Vorwärts-Verletzung — daher strikt 1.1 → 8.4 abarbeitbar.
