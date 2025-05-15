# QueryMyst

## 🔍 Risolvi Misteri con SQL

QueryMyst è una piattaforma educativa interattiva che trasforma l'apprendimento di SQL in un'avventura investigativa. Risolvi misteri e casi utilizzando query SQL, guadagna obiettivi e competi con altri investigatori nella classifica.

## ✨ Caratteristiche Principali

- 🕵️ **Misteri SQL**: Risolvi casi interattivi utilizzando interrogazioni SQL
- 📊 **Visualizzazione Schema**: Interfaccia drag-and-drop per esplorare le relazioni tra tabelle
- 🏆 **Sistema di Obiettivi**: Guadagna badge e riconoscimenti mentre migliori le tue competenze
- 📈 **Classifiche**: Competi con altri utenti per raggiungere la vetta
- 📝 **Creazione di Misteri**: Crea e condividi i tuoi enigmi SQL con la community
- 📚 **Area di Apprendimento**: Guide complete ed esempi per imparare SQL

## 🛠️ Tecnologie Utilizzate

- **Frontend**: HTML5, CSS3, JavaScript, Bootstrap 5
- **Backend**: ASP.NET Core 6.0, Razor Pages
- **Database**: Entity Framework Core, SqlServer
- **Autenticazione**: ASP.NET Identity
- **Editor SQL**: CodeMirror, Highlight.js

## 🚀 Installazione

### Prerequisiti
- .NET 6.0 SDK o superiore
- Visual Studio 2022 (opzionale)

### Passi per l'Installazione

1.  Clona il repository
    ```
    git clone https://github.com/tuonomeutente/QueryMyst.git cd QueryMyst
    ```
2.  Ripristina i pacchetti NuGet
    ```
    dotnet restore
    ```
3.  Applica le migrazioni per creare il database
    ```
    dotnet ef database update
    ```
4.  Avvia l'applicazione
    ```
    dotnet run
    ```
5.  Naviga su `https://localhost:5001` nel tuo browser

## 💻 Utilizzo

1.  **Registra un Account**: Crea un account investigatore
2.  **Esplora i Misteri**: Sfoglia l'elenco dei casi disponibili
3.  **Risolvi i Casi**: Utilizza l'editor SQL per interrogare il database e trovare indizi
4.  **Guadagna Obiettivi**: Sblocca riconoscimenti completando diverse azioni
5.  **Crea Misteri**: Condividi la tua esperienza creando nuovi enigmi

## 📂 Struttura del Progetto

-   **/Pages**: Contiene le pagine Razor dell'applicazione
    -   **/Account**: Pagine di autenticazione e gestione account
    -   **/Mysteries**: Pagine relative ai misteri SQL
    -   **/Shared**: Componenti condivisi e layout
-   **/wwwroot**: File statici (CSS, JS, immagini)
-   **/Data**: Modelli EF Core e configurazioni del database
-   **/Models**: Classi di modello dell'applicazione
-   **/Services**: Servizi dell'applicazione

## ➡️ Flusso di Apprendimento

1.  **Comprensione del Problema**: Leggi la descrizione del mistero
2.  **Esplorazione dei Dati**: Analizza lo schema del database e i dati di esempio
3.  **Formulazione della Query**: Scrivi la query SQL nell'editor
4.  **Esecuzione e Verifica**: Esegui la query e analizza i risultati
5.  **Risoluzione**: Quando la query corretta viene trovata, il mistero è risolto!

## 👋 Come Contribuire

Siamo aperti a contributi per migliorare QueryMyst! Ecco come puoi aiutare:

1.  Fork del repository
2.  Crea un nuovo branch (`git checkout -b feature/miglioramento`)
3.  Commit delle modifiche (`git commit -m 'Aggiunto nuovo mistero'`)
4.  Push al branch (`git push origin feature/miglioramento`)
5.  Apri una Pull Request

---
Sviluppato con ❤️ da Marcpad0
