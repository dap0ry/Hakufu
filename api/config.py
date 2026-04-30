from dotenv import dotenv_values
from pathlib import Path

_env = dotenv_values(Path(__file__).parent.parent / ".env", interpolate=False)


def _require(key: str) -> str:
    v = _env.get(key)
    if not v:
        raise RuntimeError(f"Falta la variable de entorno: {key}")
    return v


MONGODB_URI = _require("MONGODB_URI")
MONGODB_DB  = _env.get("MONGODB_DATABASE") or "Hakufu"

CLOUDINARY_CLOUD_NAME = _require("CLOUDINARY_CLOUD_NAME")
CLOUDINARY_API_KEY    = _require("CLOUDINARY_API_KEY")
CLOUDINARY_API_SECRET = _require("CLOUDINARY_API_SECRET")

JWT_SECRET         = _require("JWT_SECRET")
JWT_ALGORITHM      = "HS256"
JWT_EXPIRE_MINUTES = 60 * 24 * 30  # 30 días
