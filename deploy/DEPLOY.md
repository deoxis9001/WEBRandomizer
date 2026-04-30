# Déploiement WebRandomizer — Ubuntu + Apache

## Prérequis

- Serveur Ubuntu 22.04 / 24.04
- Accès root (sudo)
- Apache2 installé
- Un domaine pointant sur l'IP du serveur (ex: `randomizer.monsite.com`)

```bash
sudo apt update
sudo apt install -y apache2 certbot python3-certbot-apache
```

---

## 1. Télécharger la release

Sur GitHub → **Releases** → télécharger `WEBRandomizer-vX.X.X-linux-x64.tar.gz`

Ou directement sur le serveur :

```bash
wget https://github.com/deoxis9001/WEBRandomizer/releases/download/v1.0.0/WEBRandomizer-v1.0.0-linux-x64.tar.gz
```

---

## 2. Récupérer les fichiers de déploiement

```bash
sudo apt install -y git
git clone https://github.com/deoxis9001/WEBRandomizer --depth=1 --no-checkout /tmp/wrandom
cd /tmp/wrandom
git checkout HEAD -- deploy/
cd deploy/
```

---

## 3. Lancer l'installation

```bash
sudo bash install.sh ~/WEBRandomizer-v1.0.0-linux-x64.tar.gz randomizer.monsite.com
```

Ce script :
- Extrait l'app dans `/opt/webrandomizer/`
- Crée et démarre le service systemd
- Configure Apache en reverse proxy sur ton domaine

Vérifier que tout tourne :

```bash
sudo systemctl status webrandomizer
```

Ouvrir `http://randomizer.monsite.com` dans un navigateur pour vérifier.

---

## 4. Activer HTTPS (Let's Encrypt)

```bash
sudo certbot --apache -d randomizer.monsite.com
```

Certbot modifie automatiquement la config Apache et programme le renouvellement.

Vérifier le renouvellement automatique :

```bash
sudo certbot renew --dry-run
```

---

## 5. Mettre à jour l'application

```bash
# Télécharger la nouvelle version
wget https://github.com/deoxis9001/WEBRandomizer/releases/download/v1.1.0/WEBRandomizer-v1.1.0-linux-x64.tar.gz

# Arrêter le service
sudo systemctl stop webrandomizer

# Remplacer les fichiers
sudo tar -xzf WEBRandomizer-v1.1.0-linux-x64.tar.gz -C /opt/webrandomizer/
sudo chown -R www-data:www-data /opt/webrandomizer/

# Redémarrer
sudo systemctl start webrandomizer
sudo systemctl status webrandomizer
```

---

## 6. Commandes utiles

| Action | Commande |
|--------|----------|
| Voir les logs en direct | `sudo journalctl -u webrandomizer -f` |
| Redémarrer l'app | `sudo systemctl restart webrandomizer` |
| Arrêter l'app | `sudo systemctl stop webrandomizer` |
| Recharger Apache | `sudo systemctl reload apache2` |
| Voir les erreurs Apache | `sudo tail -f /var/log/apache2/webrandomizer-error.log` |

---

## 7. Résolution de problèmes

**Le service ne démarre pas**
```bash
sudo journalctl -u webrandomizer -n 50
```
Vérifier que le binaire est exécutable :
```bash
ls -la /opt/webrandomizer/WebRandomizer
sudo chmod +x /opt/webrandomizer/WebRandomizer
```

**Apache renvoie une erreur 502**
Le service Kestrel n'est pas démarré ou écoute sur le mauvais port.
```bash
sudo systemctl status webrandomizer
curl http://127.0.0.1:5000/api/options
```

**Erreur de permissions sur les fichiers**
```bash
sudo chown -R www-data:www-data /opt/webrandomizer/
```
