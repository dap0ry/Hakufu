from fastapi import APIRouter, HTTPException, status
from datetime import datetime, timedelta, timezone
from jose import jwt
import bcrypt
from api.models import RegisterRequest, LoginRequest, TokenResponse
from api.database import get_db
from api.config import JWT_SECRET, JWT_ALGORITHM, JWT_EXPIRE_MINUTES

router = APIRouter(prefix="/auth", tags=["auth"])


def _hash(password: str) -> str:
    return bcrypt.hashpw(password.encode(), bcrypt.gensalt()).decode()

def _verify(password: str, hashed: str) -> bool:
    return bcrypt.checkpw(password.encode(), hashed.encode())


def _make_token(username: str) -> str:
    exp = datetime.now(timezone.utc) + timedelta(minutes=JWT_EXPIRE_MINUTES)
    return jwt.encode({"sub": username, "exp": exp}, JWT_SECRET, algorithm=JWT_ALGORITHM)


@router.post("/register", response_model=TokenResponse, status_code=201)
async def register(body: RegisterRequest):
    if body.password != body.password_confirm:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Las contraseñas no coinciden")

    db = get_db()
    if await db.users.find_one({"username": body.username}):
        raise HTTPException(status.HTTP_409_CONFLICT, "Nombre de usuario ya en uso")
    if await db.users.find_one({"email": body.email}):
        raise HTTPException(status.HTTP_409_CONFLICT, "Email ya registrado")

    now = datetime.now(timezone.utc)
    await db.users.insert_one({
        "username":        body.username,
        "email":           body.email,
        "password_hash":   _hash(body.password),
        "is_profile_public": True,
        "bio":             "",
        "avatar_url":      "",
        "created_at":      now,
        "last_seen":       now,
    })
    return TokenResponse(access_token=_make_token(body.username), username=body.username)


@router.post("/login", response_model=TokenResponse)
async def login(body: LoginRequest):
    db = get_db()
    user = await db.users.find_one({"username": body.username})
    if not user or not _verify(body.password, user["password_hash"]):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Credenciales incorrectas")

    await db.users.update_one(
        {"username": body.username},
        {"$set": {"last_seen": datetime.now(timezone.utc)}},
    )
    return TokenResponse(access_token=_make_token(body.username), username=body.username)
