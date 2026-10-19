# BudgetApp

En webbapplikation byggd i **ASP.NET Core MVC** för att hantera privatekonomi med:
- autentisering (register/inloggning)
- kategorier och transaktioner per användare
- budget per kategori och månad
- dashboard med grafer, jämförelser och rapportvy (PDF via utskrift)

## Funktioner

### ✅ Grundfunktioner
- Inloggning/registrering med ASP.NET Core Identity
- Användarspecifik data (varje användare ser bara sina egna poster)
- Skapa, redigera och ta bort kategorier
- Skapa, redigera och ta bort transaktioner
- Markera transaktioner som inkomst/utgift
- Datum, belopp, beskrivning, kategori

### ✅ Dashboard
- Total inkomst, total utgift och saldo för vald period
- Utgifter per kategori (diagram)
- Netto per dag (linjediagram)
- Topp 3 största utgifter
- Senaste transaktioner
- Månadsjämförelse (vald månad vs föregående månad)

### ✅ Budget
- Budget per kategori och månad
- Snabbsätt budget för flera kategorier
- Budget vs utfall
- Varning vid över budget

### ✅ Rapport/PDF
- Rapportvy för vald månad
- “Skriv ut / Spara som PDF” från browser

---

## Teknikstack

- **.NET 10 / ASP.NET Core MVC**
- **Entity Framework Core**
- **SQL Server / LocalDB** (eller annan konfigurerad provider)
- **ASP.NET Core Identity**
- **Bootstrap 5**
- **Chart.js**

---

## Kom igång lokalt

### 1. Klona projektet
```bash
git clone https://github.com/<ditt-användarnamn>/BudgetApp.git
cd BudgetApp
```

### 2. Återställ paket
```bash
dotnet restore
```

### 3. Kör migrationer
```bash
dotnet ef database update
```

> Om du behöver skapa migration först:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Starta appen
```bash
dotnet run
```

Öppna URL:en som visas i terminalen (oftast `https://localhost:xxxx`).

---

## Projektstruktur (översikt)

- `Controllers/` –
