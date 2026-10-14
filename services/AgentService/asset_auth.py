"""
Репозиторий-управляемый helper для auth-контура tracked AI asset manifest-ов.
"""

from __future__ import annotations

import os
from typing import Mapping

SUPPORTED_AUTH_TYPES = ("env-header",)


def normalize_auth_config(config: object | None) -> dict[str, str] | None:
    if config in (None, "", {}):
        return None
    if not isinstance(config, dict):
        raise ValueError("Asset auth config must be an object")

    auth_type = str(config.get("type") or "").strip().lower()
    if not auth_type:
        raise ValueError("Asset auth config must define type")

    if auth_type == "env-header":
        env_var = str(config.get("envVar") or "").strip()
        header = str(config.get("header") or "").strip()
    else:
        raise ValueError(
            f"Unsupported asset auth type {auth_type!r}. Supported types: {', '.join(SUPPORTED_AUTH_TYPES)}"
        )

    if not env_var:
        raise ValueError(f"Asset auth {auth_type!r} must define envVar")
    if not header:
        raise ValueError(f"Asset auth {auth_type!r} must define header")

    return {
        "type": auth_type,
        "envVar": env_var,
        "header": header,
    }


def resolve_auth_headers(
    config: object | None,
    env: Mapping[str, str] | None = None,
) -> dict[str, str]:
    normalized = normalize_auth_config(config)
    if normalized is None:
        return {}

    source_env = env or os.environ
    token = str(source_env.get(normalized["envVar"]) or "").strip()
    if not token:
        raise ValueError(
            f"Asset auth requires environment variable {normalized['envVar']!r}, but it is empty or missing."
        )

    return {normalized["header"]: token}
