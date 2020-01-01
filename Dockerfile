FROM frolvlad/alpine-mono:latest AS builder

RUN adduser -S -u 99 sonarr && \
    apk add --no-cache mediainfo libmediainfo && \
    mkdir /config && \    
    chown sonarr /config && \
    mkdir -p /mnt/user/downloads/tv && \
    mkdir -p /mnt/user/tv

USER sonarr

WORKDIR /sonarr
COPY --chown=sonarr:nogroup _output/ ./

VOLUME /config
VOLUME /mnt/user/downloads/tv
VOLUME /mnt/user/tv

EXPOSE 8989

CMD ["mono", "--debug", "/sonarr/Sonarr.Console.exe", "-nobrowser", "-data=/config"]


