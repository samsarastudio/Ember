#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
if [ "$(id -u)" -ne 0 ]; then echo 'Run with sudo bash install.sh'; exit 1; fi
if [ "$(uname -m)" != aarch64 ]; then echo 'This package requires a 64-bit ARM OS (aarch64).'; exit 1; fi
test -f Api/Emberwake.Backend
command -v openssl >/dev/null
id emberwake >/dev/null 2>&1 || useradd --system --home-dir /var/lib/emberwake --shell /usr/sbin/nologin emberwake
install -d -o emberwake -g emberwake -m 750 /var/lib/emberwake
install -d -m 755 /opt/emberwake/Api
# This is an explicit manual update. Finish active matches before running it again.
systemctl stop emberwake.service 2>/dev/null || true
cp -a Api/. /opt/emberwake/Api/
chmod 755 /opt/emberwake/Api/Emberwake.Backend
if [ ! -e /etc/emberwake.env ]; then
    umask 077
    cat > /etc/emberwake.env <<EOF
ASPNETCORE_URLS=http://127.0.0.1:5080
EMBERWAKE_DB=/var/lib/emberwake/emberwake.db
EMBERWAKE_MATCH_DATA=/var/lib/emberwake/matches
EMBERWAKE_BATTLE_TRANSPORT=fusion
EMBERWAKE_REMOTE_WORKER=1
EMBERWAKE_MATCH_REGION=us
EMBERWAKE_WORKER_KEY=$(openssl rand -hex 32)
EMBERWAKE_MATCH_CAPACITY=4
EMBERWAKE_ADMIN_TOKEN=$(openssl rand -hex 32)
EMBERWAKE_ACCOUNT_KEY=$(openssl rand -hex 32)
# Set these for email verification (STARTTLS, normally port 587):
# EMBERWAKE_SMTP_HOST=smtp.your-provider.example
# EMBERWAKE_SMTP_PORT=587
# EMBERWAKE_SMTP_USER=your-user
# EMBERWAKE_SMTP_PASSWORD=your-secret
# EMBERWAKE_SMTP_FROM=login@your-verified-domain.example
# EMBERWAKE_PRIVACY_URL=https://your-published-policy
# EMBERWAKE_TERMS_URL=https://your-published-terms
EOF
fi
install -m 644 emberwake.service /etc/systemd/system/emberwake.service
install -d -m 755 /etc/caddy/sites
if [ ! -e /etc/caddy/sites/emberwake.caddy ]; then install -m 644 Caddyfile /etc/caddy/sites/emberwake.caddy; fi
systemctl daemon-reload
systemctl enable --now emberwake.service
echo 'API installed. Configure SMTP in /etc/emberwake.env and import /etc/caddy/sites/*.caddy in your existing Caddyfile.'
echo 'See README.md for DNS, HTTPS, backups and verification.'
