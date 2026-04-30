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
# Détecter le nom du service Apache
if systemctl list-units --type=service | grep -q "httpd"; then
    APACHE_SVC=httpd
else
    APACHE_SVC=apache2
fi

a2enmod proxy proxy_http headers 2>/dev/null || true
sed "s/randomizer.example.com/$DOMAIN/" apache-webrandomizer.conf \
    > /etc/apache2/sites-available/webrandomizer.conf 2>/dev/null \
    || cp apache-webrandomizer.conf /etc/httpd/conf.d/webrandomizer.conf
a2ensite webrandomizer 2>/dev/null || true
apache2ctl configtest 2>/dev/null || apachectl configtest
systemctl enable "$APACHE_SVC"
if systemctl is-active --quiet "$APACHE_SVC"; then
    systemctl reload "$APACHE_SVC"
else
    systemctl start "$APACHE_SVC"
fi
echo "Apache ($APACHE_SVC) : OK"

echo ""
echo "Déployé sur http://$DOMAIN"
echo "Logs : journalctl -u webrandomizer -f"
