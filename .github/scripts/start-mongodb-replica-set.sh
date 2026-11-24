#!/usr/bin/env bash
set -euo pipefail

mongo_admin_secret="$(openssl rand -hex 24)"
echo "::add-mask::$mongo_admin_secret"

show_mongo_diagnostics() {
  echo "::error::MongoDB replica set initialization failed"
  docker ps -a --filter name=^/mongodb$ --format '{{.Names}} {{.Status}}' || true
  docker logs --tail 100 mongodb || true
}
trap show_mongo_diagnostics ERR

docker run -d --name mongodb --publish 27017:27017 \
  --env MONGO_INITDB_ROOT_USERNAME=ci --env MONGO_INITDB_ROOT_PASSWORD="$mongo_admin_secret" \
  --entrypoint bash mongo:8.0 -c 'openssl rand -base64 756 > /tmp/keyfile && chmod 400 /tmp/keyfile && chown 999:999 /tmp/keyfile && exec docker-entrypoint.sh mongod --replSet rs0 --keyFile /tmp/keyfile --bind_ip_all'

run_mongosh() {
  timeout 10s docker exec mongodb mongosh --quiet --username ci \
    --password "$mongo_admin_secret" --eval "$1"
}

wait_for_mongo() {
  local label="$1"
  local expression="$2"
  local expected="$3"
  local deadline=$((SECONDS + 60))

  while ((SECONDS < deadline)); do
    if run_mongosh "$expression" 2>/dev/null | grep -qx "$expected"; then
      echo "$label is ready"
      return 0
    fi
    if [[ "$(docker inspect --format '{{.State.Running}}' mongodb)" != true ]]; then
      echo "MongoDB container stopped while waiting for $label" >&2
      break
    fi
    sleep 1
  done

  echo "Timed out waiting for $label; last mongosh error:" >&2
  run_mongosh "$expression" || true
  return 1
}

wait_for_mongo 'MongoDB' 'db.hello().ok' '1'
run_mongosh 'rs.initiate({_id: "rs0", members: [{_id: 0, host: "localhost:27017"}]})'
wait_for_mongo 'MongoDB primary' 'db.hello().isWritablePrimary' 'true'

# Keep credentials separate from the connection string used by the tests.
echo 'LEADERBOARD_TEST_MONGO_USER=ci' >> "$GITHUB_ENV"
echo "LEADERBOARD_TEST_MONGO_SECRET=$mongo_admin_secret" >> "$GITHUB_ENV"
