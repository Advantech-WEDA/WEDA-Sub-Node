#!/usr/bin/env bash
# Package one example for delivery: the customer gets a compose file and a README, and needs
# nothing else from this repository. Only the compose file goes onto the device.
#
# The README is written for the customer, in Traditional Chinese.
#
# Usage:
#   scripts/package-example.sh <example> <image-tag>
#   scripts/package-example.sh feature-transform-pipeline 20260911-013245
#
# Env overrides: OUTDIR (default: the example's own delivery/ directory), IMAGE_REPO (default:
# the one in the deploy compose).

set -euo pipefail

NAME="${1:?usage: $0 <example> <image-tag>}"
TAG="${2:?usage: $0 <example> <image-tag>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC="${REPO_ROOT}/examples/${NAME}"
# A delivery bundle belongs with the example it packages, not in a directory of its own at the
# repository root. The tag is in the folder name because that folder is what gets sent on.
OUTDIR="${OUTDIR:-${SRC}/delivery}"
COMPOSE="${SRC}/docker-compose.deploy.yml"
EXPECT="${SRC}/e2e-expect.json"

for f in "${COMPOSE}" "${SRC}/delivery.md" "${EXPECT}"; do
  [[ -f "$f" ]] || { echo "ERROR: missing $(basename "$f") in examples/${NAME}/" >&2; exit 1; }
done

# The container name is the one the README tells the customer to look at, and the same one the
# deployment verification uses - taken from one place so the two cannot drift.
CONTAINER=$(python -c "import json,sys;print(json.load(open(sys.argv[1],encoding='utf-8'))['container'])" "${EXPECT}")
IMAGE_LINE=$(grep -m1 -E '^[[:space:]]+image: [^$]' "${COMPOSE}" | sed 's/^ *image: //')
IMAGE_REPO="${IMAGE_REPO:-${IMAGE_LINE%:*}}"
IMAGE="${IMAGE_REPO}:${TAG}"

DEST="${OUTDIR}/${NAME}-${TAG}"
rm -rf "${DEST}"
mkdir -p "${DEST}"

# The compose header explains the file to a maintainer of this repository - the other compose
# file, the defect that dictated the network mode. None of that is the customer's context, so it
# is replaced. The services themselves are already deployable as they stand; only the image is
# pinned to the tag being delivered.
{
  cat <<HEADER
# ${NAME} - deployment compose
#
# Everything this needs is inside the image, including the WedaNode connection. Put this file on
# the device and start it; see README.md.
HEADER
  sed -n '/^services:/,$p' "${COMPOSE}"
} | sed "s|^\([[:space:]]*\)image: ${IMAGE_LINE}$|\1image: ${IMAGE}|" > "${DEST}/docker-compose.yml"

grep -q "image: ${IMAGE}$" "${DEST}/docker-compose.yml" || {
  echo "ERROR: could not pin the image line; expected '${IMAGE_LINE}' in ${COMPOSE}" >&2; exit 1; }

{
  cat "${SRC}/delivery.md"
  cat <<EOF

---

## 你需要什麼

- 一台裝得到 Docker、你連得進去的 WEDA 裝置，而且它的 WedaNode 正在跑
- 拉得到 \`${IMAGE}\` 的權限

設定檔（包含要連哪一台 WedaNode、用什麼帳密）都已經包在 image 裡，不需要另外準備。

## 怎麼跑

**1. 把 compose 檔放到裝置上**

\`\`\`bash
ssh <device> 'mkdir -p /opt/${NAME}'
scp docker-compose.yml <device>:/opt/${NAME}/
\`\`\`

裝置上只需要這一個檔案，這份 README 留在你手上就好。

**2. 確認 image 那一行**

\`docker-compose.yml\` 第一個服務寫的是：

\`\`\`yaml
    image: ${IMAGE}
\`\`\`

如果你是從別的位址拉這個 image，改這一行。

**3. 啟動**

\`\`\`bash
cd /opt/${NAME}
docker compose up -d
\`\`\`

**4. 確認它有在做事**

\`\`\`bash
docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' ${CONTAINER}
docker logs ${CONTAINER}
\`\`\`

第一行要是 \`status=running restarts=0\`，log 裡要看到：

| 這一行 | 代表 |
|---|---|
| \`NATS connection verified\` | 連上 WedaNode 了 |
| \`NATS service discovered: … WedaNodeId=<裝置 id>\` | 而且連到的是**這台**裝置的 WedaNode |
| \`Registering SubNode … with Cloud\` 或 \`Found existing SubNode registration\` | 註冊成功，或沿用先前註冊過的身分 |
| \`Reporting health: … IsHealthy=True\` | 心跳正常 |

再往下就會看到最上面那段的感測值。

**5. 停止**

\`\`\`bash
cd /opt/${NAME}
docker compose down
\`\`\`

註冊資料存在裝置的 \`/opt/Advantech/weda/node/\` 底下，停掉不會消失，所以下次啟動會沿用同一個身分。
刪掉那個目錄就會重新註冊。

## 遇到問題

**log 一直出現 \`Failed to connect to WedaNode\` 與 \`Authorization Violation\`**

容器有起來，但 image 裡帶的帳密不是這台 WedaNode 要的。兩種解法：

- 在 compose 檔旁邊建一個 \`.env\`，指定這台 WedaNode 的密碼：

  \`\`\`
  SystemConfig__WedaNode__Password=<這台 WedaNode 的密碼>
  \`\`\`

  然後 \`docker compose up -d\` 重起。

**log 出現 \`Code=409, Message=Device name already in use\`**

SubNode 的名稱在同一個 WedaNode 底下必須唯一，而這個範例沒有指定時名稱會以 \`-local\` 結尾。代表已經有另一台
裝置用同一個名稱註冊過了。在 \`.env\` 裡指定一個自己的名稱再重起：

\`\`\`
SUBNODE_NAME=TransformPipeline-Line3
\`\`\`

**log 出現 \`SocketException (98): Address already in use\`**

SubNode 的 API 固定使用 2395 埠，所以一台裝置一次只能跑一個 SubNode。先把另一個停掉。
EOF
} > "${DEST}/README.md"

# Nothing internal to this repository may reach the customer: defect ids, environment names, the
# test scenario, or paths that only exist in a checkout.
if grep -rniE 'D-[0-9]{2}|\bSIT\b|E2E|docs/wiki|deploy-verify|this repository|stack config' "${DEST}"; then
  echo "ERROR: the lines above are internal and must not ship" >&2
  exit 1
fi

echo ">> ${DEST}"
ls -1A "${DEST}"
