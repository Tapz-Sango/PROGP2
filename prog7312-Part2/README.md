# prog7312-2026-prog7312-part-1-sango-tapiwa

# SmartX

SmartX is an enterprise-grade IoT Edge Gateway and Monitoring Application built with **.NET 8**, **WPF (Windows Presentation Foundation)**, and an **ASP.NET Core Web API** backend. The system provides secure device registration, real-time telemetry processing using high-performance buffer structures, automated anomaly detection, hierarchical topology checks, and encrypted file storage.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Key Features](#key-features)
- [Project Structure](#project-structure)
- [Prerequisites & System Requirements](#prerequisites--system-requirements)
- [Getting Started & Installation](#getting-started--installation)
- [Backend API Setup](#backend-api-setup)
- [WPF Desktop Setup](#wpf-desktop-setup)
- [Code Architecture & Data Structures](#code-architecture--data-structures)
- [API Documentation](#api-documentation)
- [Security Features](#security-features)

---

## Architecture Overview

The system follows a decoupled client-server architecture:

```
+-------------------------------------------------------------------+
|                     SmartX WPF Client Application                  |
|                                                                    |
|  +--------------------+   +-------------------+   +-------------+  |
|  | GatewayMenuView    |   | SensorIngestion   |   | Telemetry   |  |
|  | (Navigation Frame) |   | View & Controller |   | StreamView  |  |
|  +--------------------+   +-------------------+   +-------------+  |
+-------------------------------------------------------------------+
                              |
                        HTTP / Multipart
                        (ApiService)
                              |
                              v
+-------------------------------------------------------------------+
|                  SmartX ASP.NET Core Web API                       |
|                      (http://localhost:5245)                       |
|                                                                    |
|   +---------------------+        +----------------------------+    |
|   | Sensor Ingestion    |        | Cryptographic File Stream  |    |
|   | & Health Endpoints  |        | (AES-256 / Encrypted Logs) |    |
|   +---------------------+        +----------------------------+    |
+-------------------------------------------------------------------+
```

---

## Key Features

- **Dynamic Single-Window Navigation**: Utilizes a single `ContentControl` (`MainContentFrame`) in `MainWindow.xaml` to swap views without spawning pop-up windows.
- **Device Ingestion & Regex Gatekeeper**: Enforces standard hardware MAC address validation (`XX:XX:XX:XX:XX:XX`) before registering devices.
- **AES-256 Encrypted Stream Uploads**: Receives device log/config files via multipart HTTP POST requests and scrambles them into `.enc` files on the server using `CryptoStream`.
- **High-Throughput Telemetry Buffering**: Employs generic collections (`TelemetryPacket<T>`) and multi-dimensional Jagged Arrays (`double[][]`) to handle variable-length data bursts efficiently.
- **Operator Overloading**: Overloads mathematical operators (e.g., `+` on `PowerMeterValue`) for intuitive domain calculations.
- **Recursive Hierarchy Validation**: Validates parent-child deployment nodes (`DeploymentNode`) up to the root hub using recursive algorithms with explicit base cases.

---

## Project Structure

```
SmartX/
├── SmartX/                                    # WPF Desktop Client Project
│   ├── Collections/
│   │   └── TelemetryBufferCollection.cs       # Generic collections & buffer handling
│   ├── Models/
│   │   ├── DeploymentNode.cs                  # Recursive tree validation model
│   │   └── SensorRegistration.cs              # Sensor model & MAC regex logic
│   ├── Services/
│   │   └── ApiService.cs                      # HttpClient wrapper for backend API communication
│   ├── Views/
│   │   ├── GatewayMenuView.xaml               # Main landing dashboard
│   │   ├── GatewayMenuView.xaml.cs            # View navigation handlers
│   │   ├── SensorIngestionView.xaml           # Device ingestion & file upload UI
│   │   ├── SensorIngestionView.xaml.cs        # Ingestion business logic
│   │   ├── TelemetryStreamView.xaml           # Live telemetry analysis view
│   │   └── TelemetryStreamView.xaml.cs        # Operator overloading & jagged arrays
│   ├── App.xaml / App.xaml.cs                 # Application startup & theme initialization
│   └── MainWindow.xaml / MainWindow.xaml.cs   # Host container & frame navigator
│
└── SmartX.Api/                                # ASP.NET Core Web API Backend
    ├── Uploads/                               # Storage target for encrypted (.enc) payloads
    ├── Program.cs                             # Minimal API endpoints & AES encryption pipelines
    └── appsettings.json                       # API configuration settings
```

---

## Prerequisites & System Requirements

- **Operating System**: Windows 10/11 (Required for WPF UI execution)
- **IDE**: [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (v17.4 or newer)
- **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Workloads Required in Visual Studio**:
  - `.NET desktop development`
  - `ASP.NET and web development`

---

## Getting Started & Installation

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/YourUsername/SmartX.git
   cd SmartX
   ```

2. **Open the Solution**:

   Launch `SmartX.sln` in Visual Studio 2022.

3. **Configure Multiple Startup Projects**:

   - Right-click the Solution in **Solution Explorer** → **Set Startup Projects...**
   - Select **Multiple startup projects**.
   - Set `SmartX.Api` to **Start**.
   - Set `SmartX` to **Start**.
   - Click **Apply** and **OK**.

---

## Backend API Setup

The backend API runs on `.NET 8 Minimal APIs`.

- **HTTP Port**: `http://localhost:5245`
- **HTTPS Port**: `https://localhost:7289`

To run the API manually via CLI:

```bash
cd SmartX.Api
dotnet run
```

---

## WPF Desktop Setup

The desktop application connects to the API endpoint defined in `SmartX/Services/ApiService.cs`:

```csharp
private readonly string _baseUrl = "http://localhost:5245";
```

To run the WPF application manually via CLI:

```bash
cd SmartX
dotnet run
```

---

## Code Architecture & Data Structures

| Feature / Rubric Requirement | File Location | Class / Method | Description |
| --- | --- | --- | --- |
| **Startup Navigation** | `MainWindow.xaml` | `MainContentFrame` | Host `ContentControl` for view transitions. |
| **Data Ingestion Validation** | `SensorIngestionView.xaml.cs` | `BtnRegister_Click` | Enforces Regex: `^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$`. |
| **API Communication** | `ApiService.cs` | `UploadDeviceFileAsync` | Sends multipart form data payloads to backend. |
| **Stream Encryption** | `SmartX.Api/Program.cs` | `CryptoStream` | Encrypts uploaded files on disk using AES-256. |
| **Dynamic Data Structure** | `SensorIngestionView.xaml.cs` | `List<SensorRegistration>` | Provides dynamic O(1) list access for grid binding. |
| **Generics** | `TelemetryBufferCollection.cs` | `TelemetryPacket<T>` | Type-safe generic data container. |
| **Jagged Arrays** | `TelemetryStreamView.xaml.cs` | `double[][]` | Efficient storage for variable-length batch payloads. |
| **Operator Overloading** | `TelemetryStreamView.xaml.cs` | `operator +` | Aggregates power meter values seamlessly. |
| **Recursion** | `DeploymentNode.cs` | `RecursiveValidateHierarchy` | Validates parent-child nodes up to base case (`node == null`). |

---

## API Documentation

### File Upload & Encryption Endpoint

- **Endpoint**: `POST /api/sensors/{mac}/upload`
- **Content-Type**: `multipart/form-data`
- **Parameters**:
  - `mac` (route parameter): Device MAC Address.
  - `file` (form file): Target log/configuration document.
- **Response**: `200 OK`

```json
{
  "message": "File uploaded and encrypted successfully.",
  "filename": "00-1B-44-11-3A-B7_a1b2c3d4_config.log.enc"
}
```

---

## Security Features

1. **Input Sanitization**: MAC parameters are sanitized (`safeMac`) to prevent directory traversal attacks.
2. **At-Rest Encryption**: Ingress files are processed through standard AES-256 cipher streams, generating protected `.enc` files inside the server's `Uploads` directory.
3. **Anti-Forgery Safeguards**: Explicit configuration for API endpoint file stream processing.

---

## Youtube link: https://youtu.be/B1Pr9fA98iU 


---

## AI Usage Declaration

Generative AI tools, namely Gemini AI, ChatGPT, and DeepSeek, were utilized in order to design the app UI, structure the architecture, format the project README documentation, and provide guidance on error handling best practices and troubleshooting common issues during development. Assistance included generating and refining UI design concepts, advising on the overall architectural structure of the application, structuring and formatting the project README documentation, recommending error handling best practices and helping to diagnose and resolve common issues encountered throughout the development process. All AI-generated content was used as a reference point and guideline to ensure questions were answered as accurately as possible and alignment with project requirements. It is important to note that AI was used solely for assistance and drafting purposes only. The final assignment outcome remains the decisions, research and design decisions by me(Tapiwa Sango ST10276025).

---


