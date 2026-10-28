FROM nginx:stable-alpine@sha256:47928bf67eb62a1863cb08d9ecb52d3e704304ba614eaee17884204677702951
# Only the core HTTP reverse proxy is used. Remove unused interpreters/modules and
# their image/XML libraries; refresh remaining OS fixes before scanning the image.
RUN apk del --no-cache curl nginx-module-acme nginx-module-geoip nginx-module-image-filter nginx-module-njs nginx-module-xslt \
    && apk upgrade --no-cache
USER 101:101
