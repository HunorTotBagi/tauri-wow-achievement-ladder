# Tauri Achievement Ladder

This repository contains a .NET console application that builds a **local achievement ladder** for characters on the Tauri WoW private servers.

The application:

- Reads character lists from JSON files for multiple realms (Evermoon, Tauri, WoD)
- Calls the **Tauri API** to retrieve achievement points for each character
- Aggregates and sorts all characters into a **combined ranked ladder** and prints it to the console

---

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/en-us/download) (7.0 or later recommended)
- Valid **Tauri API credentials**:
  - API key
  - Secret

---

## Configuration

The application uses an `appsettings.Secrets.json` file to store sensitive configuration values.  
This file is **not** checked into source control and must be created manually.

### 1. File Location

Create the file in the **project root** (the same directory as your `.csproj`):

```
└─ Data/
   ├─ evermoon.txt
   ├─ tauri.txt
   └─ wod.txt
├─ appsettings.Secrets.example.json
├─ appsettings.Secrets.json   <-- create this file here
├─ Program.cs
```

In a newly created `appsetting.Secrets.json` file paste the following code

```json
{
    "TauriApi": {
        "BaseUrl": "http://chapi.tauri.hu/apiIndex.php",
        "ApiKey": "YOUR_REAL_API_KEY_HERE",
        "Secret": "YOUR_REAL_SECRET_HERE"
    }
}
```
and change your secret values accordingly. Here you can request your secret keys.

<img width="868" height="561" alt="image" src="https://github.com/user-attachments/assets/392b6334-32de-4b43-9a9e-a500606d8e39" />



# How It Works

## 1. Load Configuration
The application reads API credentials (Base URL, API Key, Secret) from the `appsettings.Secrets.json` file located in the project root.

## 2. Load Characters
The program loads character names from all three realm data files located in the `Data/` directory:

- `evermoon.txt`
- `tauri.txt`
- `wod.txt`

These files are combined into a unified in-memory list.

## 3. Call the Tauri API
For each character, the application performs a POST request to the Tauri API using the following body:

```json
{
  "secret": "<secret>",
  "url": "character-achievements",
  "params": {
    "r": "<realm>",
    "n": "<name>"
  }
}
```

The end result should look something like this in your terminal

<img width="346" height="401" alt="image" src="https://github.com/user-attachments/assets/ef3fd2fc-941b-484a-8069-6e00ecc76c96" />

