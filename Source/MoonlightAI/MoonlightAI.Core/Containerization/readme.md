# Moonlight LLM API Server Container

Generate an Ollama server using Codellama 13b-instruct.
```
$ docker build -t moonlight-llm-server:latest .
```

MoonlightAI will auto-launch the container when needed, but you can also run it manually.
The container can be manually run with the following command:
```
docker run -d --gpus all -p 11434:11434 --name moonlight-llm-server moonlight-llm-server
```