# Usa Alpine o un'altra immagine base
FROM alpine:3.18

# Installa le dipendenze necessarie
RUN apk add --no-cache \
    libstdc++ \
    libc6-compat \
    libgcc \
    libcurl \
    openssl \
    curl \
    bash

# Configura la directory di lavoro
WORKDIR /app

# Copia i file della build Unity
COPY ./Builds/Dedicated-Server-Linux/ /app/

# Espone le porte richieste
EXPOSE 7777/udp
EXPOSE 27015/tcp

# Avvia il server e accedi ai file dal volume condiviso
CMD ["./AIWatch", "-batchmode", "-nographics", "-logfile", "/app/log.txt"]
