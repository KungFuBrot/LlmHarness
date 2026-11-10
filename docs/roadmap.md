# Roadmap

## M0 – Grundgerüst ✅
- Nachrichtenmodell (Text, Werkzeugaufruf, Werkzeugergebnis), `ILlmProvider`, `ITool`, `IPermissionPolicy`
- `AgentLoop` mit parallelen Aufrufen, Fehlerbehandlung, Freigaben, `MaxTurns`, Ereignisstrom
- Tests mit `ScriptedProvider`

## M1 – Erster echter Provider
- `Harness.Providers.Anthropic` auf Basis des offiziellen C#-SDK (NuGet `Anthropic`), Standardmodell `claude-opus-5-5`
- Streaming, `refusal`- und `max_tokens`-Stop-Gründe sauber behandeln
- Konsolen-Client (`Harness.Cli`) mit Live-Ausgabe der Ereignisse

## M2 – Werkzeuge und Freigaben
- Werkzeug-Schemas aus C#-Typen erzeugen und Eingaben vor der Ausführung validieren
- Eingebaute Werkzeuge: Dateien lesen/schreiben in einem Sandbox-Ordner, Shell mit Whitelist
- Interaktive Freigabe im Terminal (einmal / immer / nie)

## M3 – Kontext
- Token-Zählung und Budget pro Lauf
- Prompt-Caching (stabiler Präfix: Werkzeuge, System-Prompt, Verlauf)
- Kontext verdichten, wenn der Verlauf zu groß wird

## M4 – Nachvollziehbarkeit
- Läufe als JSONL aufzeichnen und wieder abspielen
- OpenTelemetry-Spans pro Modell- und Werkzeugaufruf
- Kleine Evaluationssuite: feste Aufgaben, Erfolgsquote, Kosten pro gelöster Aufgabe

## M5 – Mehrere Agenten
- Unteragenten als Werkzeug (Aufgabe delegieren, Ergebnis zusammenfassen)
- Einsatz im Snapmaker-Projekt
