# Base Ollama image (includes CUDA support for NVIDIA GPUs)
FROM ollama/ollama:latest

LABEL purpose="GPU-accelerated CodeLlama image for C# code generation"

# Set up environment for NVIDIA GPU support
ENV NVIDIA_VISIBLE_DEVICES=all
ENV NVIDIA_DRIVER_CAPABILITIES=compute,utility
ENV OLLAMA_MODELS=/root/.ollama/models

# Pre-pull the CodeLlama model. 13b-instruct is good for C# code gen and will fit on my RTX3060
# 34b-instruct is better but requires more VRAM, requiring a better GPU.
# 7b-instruct is faster but less capable, and since we run at night, speed is less of a concern.
RUN ollama serve & sleep 5 && ollama pull codellama:13b-instruct && pkill ollama

# Expose Ollama's API port
EXPOSE 11434

# Launch Ollama server automatically
ENTRYPOINT ["ollama", "serve"]