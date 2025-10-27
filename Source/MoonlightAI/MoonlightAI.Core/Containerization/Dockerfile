# Base Ollama image (includes CUDA support for NVIDIA GPUs)
FROM ollama/ollama:latest

LABEL purpose="GPU-accelerated Ollama server with volume-mounted model storage"

# Set up environment for NVIDIA GPU support
ENV NVIDIA_VISIBLE_DEVICES=all
ENV NVIDIA_DRIVER_CAPABILITIES=compute,utility
ENV OLLAMA_MODELS=/root/.ollama/models

# Note: Models are stored in a volume-mounted directory from the host.
# This allows switching between models without rebuilding the image.
# Models are automatically downloaded by the application if not present.

# Expose Ollama's API port
EXPOSE 11434

# Launch Ollama server automatically
ENTRYPOINT ["ollama", "serve"]