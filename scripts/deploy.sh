#!/usr/bin/env bash
set -euo pipefail

IMAGE_NAME="${IMAGE_NAME:-rate-limiter}"
TAG="${TAG:-latest}"
REGISTRY="${REGISTRY:-}"

echo "Building Docker image: ${IMAGE_NAME}:${TAG}"
docker build -t "${IMAGE_NAME}:${TAG}" -f docker/Dockerfile .

if [ -n "${REGISTRY}" ]; then
  echo "Pushing image to ${REGISTRY}"
  docker tag "${IMAGE_NAME}:${TAG}" "${REGISTRY}/${IMAGE_NAME}:${TAG}"
  docker push "${REGISTRY}/${IMAGE_NAME}:${TAG}"
fi

echo "Deployment complete."
