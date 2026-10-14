FROM python:3.12-slim-bookworm AS build
ENV CMAKE_ARGS="-DGGML_CUDA=OFF -DGGML_NATIVE=OFF -DGGML_BLAS=ON -DGGML_BLAS_VENDOR=OpenBLAS" FORCE_CMAKE=1 CMAKE_BUILD_PARALLEL_LEVEL=2
RUN apt-get update && apt-get install -y --no-install-recommends build-essential cmake pkg-config libopenblas-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /wheels
RUN pip wheel --wheel-dir=/wheels llama-cpp-python==0.3.16
COPY requirements.txt requirements-runtime.txt ./
RUN pip wheel --find-links=/wheels --wheel-dir=/wheels -r requirements.txt -r requirements-runtime.txt
FROM python:3.12-slim-bookworm
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 libopenblas0-pthread ffmpeg && rm -rf /var/lib/apt/lists/*
COPY --from=build /wheels /wheels
RUN pip install --no-cache-dir --no-index --find-links=/wheels -r /wheels/requirements.txt -r /wheels/requirements-runtime.txt && rm -rf /wheels
WORKDIR /app
ENV PYTHONUNBUFFERED=1 OMP_NUM_THREADS=4 OPENBLAS_NUM_THREADS=4 AI_LLM_GPU_LAYERS=0 AI_ASR_DEVICE=cpu AI_ASR_COMPUTE_TYPE=int8 AI_REQUIRE_ADAPTER=true
COPY . .
EXPOSE 8100
CMD ["python", "bootstrap_runtime.py"]
