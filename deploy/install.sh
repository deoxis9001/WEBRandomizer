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
chown -R apache:apache "$INSTALL_DIR"

# Service systemd
sed "s/User=www-data/User=apache/" webrandomizer.service > /etc/systemd/system/webrandomizer.service
systemctl daemon-reload
systemctl enable webrandomizer
systemctl restart webrandomizer
echo "Service systemd : OK"

# Apache (httpd)
sed "s/randomizer.example.com/$DOMAIN/" apache-webrandomizer.conf \
    > /etc/httpd/conf.d/webrandomizer.conf
systemctl enable httpd
if systemctl is-active --quiet httpd; then
    systemctl reload httpd
else
    systemctl start httpd
fi
echo "Apache (httpd) : OK"

echo ""
echo "Déployé sur http://$DOMAIN"
echo "Logs : journalctl -u webrandomizer -f"
