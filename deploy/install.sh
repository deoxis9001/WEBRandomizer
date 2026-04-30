#!/bin/bash
# Usage: sudo bash install.sh <archive.tar.gz> [domaine]
# Ex:    sudo bash install.sh WEBRandomizer-v1.0.0-linux-x64.tar.gz randomizer.monsite.com
set -e

ARCHIVE=${1:?"Usage: $0 <archive.tar.gz> [domaine]"}
DOMAIN=${2:-randomizer.example.com}
INSTALL_DIR=/opt/webrandomizer

echo "=== Installation WebRandomizer ==="

# Extraction
mkdir -p "$INSTALL_DIR"
tar -xzf "$ARCHIVE" -C "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/WebRandomizer"
chown -R www-data:www-data "$INSTALL_DIR"

# Service systemd
cp webrandomizer.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable webrandomizer
systemctl restart webrandomizer
echo "Service systemd : OK"

# Apache
a2enmod proxy proxy_http headers
sed "s/randomizer.example.com/$DOMAIN/" apache-webrandomizer.conf \
    > /etc/apache2/sites-available/webrandomizer.conf
a2ensite webrandomizer
apache2ctl configtest
systemctl reload apache2
echo "Apache : OK"

echo ""
echo "Déployé sur http://$DOMAIN"
echo "Logs : journalctl -u webrandomizer -f"
