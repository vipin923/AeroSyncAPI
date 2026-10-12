# AeroSyncAPI - Flight Itinerary & Layover Matching Backend

A real-time data extraction and passenger matching backend built in C# to parse boarding pass screenshots, confirmation emails, and coordinate airport layovers.

## Technical Architecture
* **Framework:** .NET 8 / ASP.NET Core
* **Language:** C#
* **Real-Time Communication:** SignalR for asynchronous passenger feed updates (`TerminalHub`).
* **Database:** SQL Server
* **Data Access:** Optimized stored procedures and Dapper for low-latency retrieval of parsed flight data.
* **Security:** PASETO token authentication, AES-256 field encryption for sensitive passenger identifiers, deterministic SHA-256 email hashing, and BCrypt password hashing.

## Core Features
* **Multi-Modal Data Extraction:** Processes unstructured flight confirmation text and boarding pass images (via memory streams with strict size guards) to extract PNR, origin, destination, and flight times.
* **Smart Layover Matching:** Calculates real-time availability and matches active airport passengers using custom algorithms.
* **Enterprise Security:** Prevents SQL injection using parameterized queries and structured data handling.
