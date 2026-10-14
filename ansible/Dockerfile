FROM python:3.12-slim-bookworm
RUN pip install --no-cache-dir ansible-core==2.19.3 kubernetes==34.1.0 PyYAML==6.0.3 jsonpatch==1.33 requests==2.32.5
RUN ansible-galaxy collection install kubernetes.core:6.5.0
WORKDIR /controller
COPY ansible/ /controller/ansible/
COPY scripts/seed-local.py /controller/seed-local.py
CMD ["ansible-playbook", "ansible/local.yml"]
