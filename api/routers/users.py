from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, Form, status
import asyncio
from datetime import datetime, timezone
import cloudinary
import cloudinary.uploader
from api.models import PublicProfile, CurrentlyReadingItem, LibrarySyncRequest
from api.database import get_db
from api.deps import get_current_user
from api.config import CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, CLOUDINARY_API_SECRET

cloudinary.config(
    cloud_name=CLOUDINARY_CLOUD_NAME,
    api_key=CLOUDINARY_API_KEY,
    api_secret=CLOUDINARY_API_SECRET,
)

router = APIRouter(prefix="/users", tags=["users"])


def _compute_currently_reading(lib: dict) -> CurrentlyReadingItem | None:
    mangas    = lib.get("mangas", [])
    progress  = lib.get("reading_progress", [])
    if not progress or not mangas:
        return None
    # Most recently read
    def _dt(p):
        v = p.get("last_read")
        if isinstance(v, datetime):
            return v
        return datetime.min.replace(tzinfo=timezone.utc)
    latest = max(progress, key=_dt)
    manga  = next((m for m in mangas if m.get("id") == latest.get("manga_id")), None)
    if not manga:
        return None
    return CurrentlyReadingItem(
        manga_title     = manga.get("title", ""),
        manga_cover_url = manga.get("cover_cloudinary_url", ""),
        current_page    = latest.get("current_page", 0),
        total_pages     = manga.get("total_pages", 0),
    )


@router.get("/{username}", response_model=PublicProfile)
async def get_public_profile(username: str):
    db   = get_db()
    user = await db.users.find_one({"username": username})
    if not user:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Usuario no encontrado")
    if not user.get("is_profile_public", True):
        raise HTTPException(status.HTTP_403_FORBIDDEN, "Este perfil es privado")

    lib = await db.user_libraries.find_one({"username": username}) or {}
    return PublicProfile(
        username          = user["username"],
        bio               = user.get("bio", ""),
        avatar_url        = user.get("avatar_url", ""),
        created_at        = user["created_at"],
        mangas_count      = len(lib.get("mangas", [])),
        reading_history   = lib.get("reading_history", []),
        total_usage_seconds = lib.get("total_usage_seconds", 0),
        currently_reading = _compute_currently_reading(lib),
    )


@router.put("/me/library", status_code=204)
async def sync_library(
    body: LibrarySyncRequest,
    me: str = Depends(get_current_user),
):
    db = get_db()
    await db.user_libraries.update_one(
        {"username": me},
        {"$set": {**body.model_dump(), "username": me, "updated_at": datetime.now(timezone.utc)}},
        upsert=True,
    )


@router.post("/me/avatar")
async def upload_avatar(
    file: UploadFile    = File(...),
    me: str             = Depends(get_current_user),
):
    data = await file.read()
    if len(data) > 2 * 1024 * 1024:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "La imagen no puede superar los 2 MB")
    allowed = {"image/jpeg", "image/png", "image/gif"}
    if file.content_type not in allowed:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Formato no soportado. Usa JPG, PNG o GIF")
    public_id = f"hakufu/{me}/avatar"
    result = await asyncio.to_thread(
        cloudinary.uploader.upload,
        data,
        public_id=public_id,
        overwrite=True,
        resource_type="image",
    )
    url: str = result["secure_url"]
    db = get_db()
    await db.users.update_one({"username": me}, {"$set": {"avatar_url": url}})
    return {"avatar_url": url}


@router.get("/me/library")
async def download_library(me: str = Depends(get_current_user)):
    db  = get_db()
    lib = await db.user_libraries.find_one({"username": me})
    if not lib:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "No hay datos sincronizados")
    lib.pop("_id",        None)
    lib.pop("username",   None)
    lib.pop("updated_at", None)
    return lib


@router.post("/me/cover/{collection_slug}/{manga_slug}")
async def upload_manga_cover(
    collection_slug: str,
    manga_slug: str,
    manga_id: str        = Form(...),
    file: UploadFile     = File(...),
    me: str              = Depends(get_current_user),
):
    db   = get_db()
    data = await file.read()
    public_id = f"hakufu/{me}/{collection_slug}/{manga_slug}"
    result = await asyncio.to_thread(
        cloudinary.uploader.upload,
        data,
        public_id=public_id,
        overwrite=True,
        resource_type="image",
    )
    url: str = result["secure_url"]
    await db.user_libraries.update_one(
        {"username": me, "mangas.id": manga_id},
        {"$set": {"mangas.$.cover_cloudinary_url": url}},
    )
    return {"cover_url": url}


@router.post("/me/manga/{manga_id}/cover")
async def upload_cover(
    manga_id: str,
    file: UploadFile = File(...),
    me: str = Depends(get_current_user),
):
    data   = await file.read()
    result = cloudinary.uploader.upload(
        data,
        public_id=f"hakufu/{me}/{manga_id}",
        overwrite=True,
        resource_type="image",
    )
    url: str = result["secure_url"]

    db = get_db()
    await db.user_libraries.update_one(
        {"username": me, "mangas.id": manga_id},
        {"$set": {"mangas.$.cover_cloudinary_url": url}},
    )
    return {"cover_url": url}
