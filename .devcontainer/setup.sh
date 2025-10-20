#!/usr/bin/env bash
set -e
apt-get update
apt-get install -y tesseract-ocr tesseract-ocr-eng tesseract-ocr-bul libtesseract-dev
# PDFium runtime will be pulled via NuGet/native bindings as needed
npm --version || true
dotnet --info
