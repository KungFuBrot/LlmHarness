# LlmHarness

**Eine eigene Agenten-Harness für Sprachmodelle, in C# / .NET 10.** Gemeint ist die Schicht, die aus einem
Sprachmodell einen Agenten macht: Werkzeuge anbieten, Aufrufe ausführen, Ergebnisse zurückgeben,
Kontext verwalten, Freigaben einholen und alles nachvollziehbar protokollieren.

> Status: **Grundgerüst (M0)**. Agentenschleife, Werkzeug- und Freigabemodell sowie Tests stehen. Der erste echte Provider folgt.
> Referenzprojekt. Ziel ist, die Mechanik selbst zu bauen und zu verstehen, statt ein Framework zu benutzen.

## Aufbau

```
              ┌──────────── AgentLoop ─────────────┐
 Nutzer ──►   │  Modell fragen ──► Werkzeugaufrufe │ ──► Ereignisse (CLI, Logs, Tracing)
              │       ▲              │  Freigabe?   │
              │       └── Ergebnisse ◄┘  ausführen   │
              └──────────────┬─────────────────────┘
                             │ ILlmProvider
               Anthropic · weitere Anbieter · ScriptedProvider (Tests)
```

- **Provider-neutral:** Die Schleife kennt nur `ILlmProvider`. Anbieter-Adapter sind eigene Projekte.
- **Nur anhängen:** Der Verlauf wird fortgeschrieben, nie umgeschrieben. Das ist wichtig für Prompt-Caching und nachvollziehbare Läufe.
- **Parallele Werkzeugaufrufe:** Alle Aufrufe eines Zuges laufen gleichzeitig, ihre Ergebnisse gehen in einer Nachricht zurück.
- **Fehler sind Ergebnisse:** Unbekannte Werkzeuge, Ausnahmen und verweigerte Freigaben gehen als `IsError` an das Modell zurück, statt den Lauf abzubrechen.
- **Grenzen:** `MaxTurns` verhindert endlose Schleifen.

## Starten

```bash
dotnet test
```

## Roadmap

Siehe [docs/roadmap.md](docs/roadmap.md).

## Anwendung

Die Harness ist die Grundlage für den Agenten in [SnapmakerPhysicalAI](https://github.com/KungFuBrot/SnapmakerPhysicalAI):
Dort werden Maschinenbefehle als Werkzeuge registriert, und gefährliche Aktionen laufen über die Freigabe-Policy.
