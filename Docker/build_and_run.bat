@echo off
REM Cancella il container esistente se è in esecuzione
docker stop unity-ai-watch-server
docker rm unity-ai-watch-server

REM (Opzionale) Rimuove l'immagine Docker precedente
docker rmi unity-ai-watch-server

REM Cancella la cartella di build, se necessario (facoltativo)
REM rmdir /s /q Build

REM Costruisce la nuova immagine Docker
docker build --platform linux/x86_64 -t unity-ai-watch-server .

REM Avvia il nuovo container con la build
docker run -d ^
  --name unity-ai-watch-server ^
  -p 7777:7777/udp ^
  -p 8888:8888/udp ^
  -p 5000-5500:5000-5500/udp ^
  -p 27015:27015/tcp ^
  -p 55000-55063:55000-55063/tcp ^
  -v "%cd%\sensors:/app/sensors" ^
  unity-ai-watch-server

REM Stampa un messaggio di successo
echo Build and container started successfully!
pause