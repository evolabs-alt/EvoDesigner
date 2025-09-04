# Label Designer Application

A C# WPF desktop app for designing and printing labels with text, images, barcodes (EAN-13, EAN-8), and QR codes. Supports dynamic data binding from MySQL queries and template management via XML files.

## Features
- Drag-and-drop label designer
- Text formatting (font, size, style)
- Image import (local files)
- Barcode (EAN-13, EAN-8) and QR code support
- MySQL data binding
- Print preview and printing
- Save/load templates as XML

## Project Structure
- UI/
- Models/
- Services/
- Templates/

## Getting Started
1. Open the solution in Visual Studio or VS Code
2. Restore NuGet packages
3. Build and run the application

## Dependencies
- MySql.Data
- QRCoder
- ZXing.Net
- System.Drawing.Common
