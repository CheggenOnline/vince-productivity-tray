# Vince Utility — installasjon

Del denne mappen med en kollega, eller send lenken til [nedlastingssiden på GitHub](https://github.com/CheggenOnline/vince-productivity-tray/releases).

Mappen inneholder bare denne veiledningen og to installasjonsfiler for samme versjon:

På GitHub lagres selve filene under Releases. Lenkene nedenfor er klargjort for **0.9.15**, men virker først når versjonen er publisert. Inntil da bruker du installasjonsfilene i denne lokale mappen:

- [Windows Intel/AMD (x64)](https://github.com/CheggenOnline/vince-productivity-tray/releases/download/v0.9.15/VinceProductivityTray-0.9.15-win-x64-Setup.exe)
- [Windows ARM64](https://github.com/CheggenOnline/vince-productivity-tray/releases/download/v0.9.15/VinceProductivityTray-0.9.15-win-arm64-Setup.exe)

| PC | Velg filen som slutter med |
| --- | --- |
| Windows med Intel- eller AMD-prosessor | `win-x64-Setup.exe` |
| Windows med ARM-prosessor, for eksempel Snapdragon | `win-arm64-Setup.exe` |

Se **Innstillinger → System → Om → Systemtype** hvis du er usikker.

1. Avslutt en eventuell tidligere versjon med **Exit** i tray-menyen. Lagre notater og tidsregistreringer, og stopp opptak først.
2. Kjør riktig installasjonsfil og bekreft med `Y`. Administratorrettigheter er normalt ikke nødvendig.
3. Start **Vince Productivity Tray** fra Start-menyen etter at installasjonen er ferdig.
4. Åpne **Settings** for å velge funksjoner og snarveier. Automatisk møteopptak og Time Capture er av som standard.

Oppsettet bruker `VinceUtility` i brukerens egen **OneDrive - Vince AS**, med undermappene Quick Notes, Meeting Recordings og Time Capture. Eksisterende innstillinger og data beholdes ved oppgradering. Filene i disse mappene kan synkroniseres til bedriftens OneDrive.

Installasjonsfilene distribueres usignert. Windows eller bedriftens IT-policy kan derfor blokkere dem; avklar eventuell blokkering med IT. **Download update** i appen laster ned riktig oppdateringsfil og kontrollerer SHA-256. Brukeren starter selv installasjonen; appen kjører den ikke automatisk.

På GitHub kan nyeste versjon være merket **Pre-release**. Velg versjonen med høyest nummer og riktig prosessortype. Du trenger bare én installasjonsfil, ikke kildekoden.
