# FiskalyMock

Simulatore di middleware fiscale per GianoITA. Tre modalita' (radio in alto):

| Modalita' | Porta default | Protocollo | `GET /state` |
|---|---|---|---|
| IT | 8180 | JSON Fiskaly (`/api/receipt`, ...) | `<Country>IT</Country>` |
| DE | 5618 | XML EFR (`/register`, `/register/void`, ...) | `<Country>DE</Country>` |
| AT | 5618 | XML EFR (stessi endpoint del DE) | `<Country>AT</Country>` + `<Company>ATU57780814</Company>` + una smart card in `<SC>` |

In Austria Giano pretende almeno una smart card installata **oppure** la company di test efsta
`ATU57780814` (`EfrClient.TestCloudCompanyId`), altrimenti il test EFR all'avvio fallisce; il mock
manda entrambe. Il payload austriaco differisce dal tedesco (niente `TraS`/`TID`, presenti `D`, `TN`,
`AT_Storno`, `TaxA` con `Prc` invece di `TaxG`), ma gli endpoint sono gli stessi.

Nota: con il mock va tenuto **spento** in Giano il flag "Riavvia servizio all'avvio"
(Admin -> EFR). Il mock e' un processo normale, non il servizio Windows "EFR": con il flag
acceso Giano fallisce il riavvio e mostra il quadrato EFR rosso in start page.

per buildare

taskkill /F /IM FiskalyMock.exe

dotnet clean -c Release

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true