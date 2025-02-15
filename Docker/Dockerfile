# Usa Debian o un'altra immagine base compatibile con piattaforma x86_64
FROM debian:bookworm-slim

# Installa le librerie necessarie per AIWatch Server
RUN apt-get update && apt-get install -y \
    libstdc++6 \
    libc6 \
    libgcc1 \
    libcurl4 \
    inotify-tools \
    libc-bin \
    openssl \
    curl \
    bash \
    && rm -rf /var/lib/apt/lists/*

# Configura la directory di lavoro
WORKDIR /app

# Copia i file della build Unity nel container
COPY ./Build/ /app/

# Assegna i permessi di esecuzione al server Unity
RUN chmod +x /app/AIWatch.x86_64

# Espone le porte per Netcode for GameObjects e Debugger
EXPOSE 7777/udp
EXPOSE 8888/udp
EXPOSE 5000-5500/udp
EXPOSE 27015/tcp
EXPOSE 55000-55063/tcp

# Avvia il server Unity
ENTRYPOINT ["./AIWatch.x86_64", "-batchmode", "-nographics"]
