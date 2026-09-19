#!/usr/bin/env bash
# Download integral dos 3 ficheiros MaleCNS v1.0 aprovados (CC BY 4.0) + SHA-256.
set -u
BASE="https://storage.googleapis.com/flyem-male-cns/v1.0/connectome-data/flat-connectome"
FILES="body-annotations-male-cns-v1.0-minconf-0.5.feather body-neurotransmitters-male-cns-v1.0.feather connectome-weights-male-cns-v1.0-minconf-0.5.feather"
for f in $FILES; do
  echo "[$(date -Is)] start $f"
  curl -sSL --retry 5 --retry-delay 5 -C - -o "$f" "$BASE/$f" || { echo "FAIL $f"; exit 1; }
  echo "[$(date -Is)] done $f $(stat -c %s "$f") bytes"
done
sha256sum $FILES > SHA256SUMS.txt
echo "[$(date -Is)] all done"; cat SHA256SUMS.txt
