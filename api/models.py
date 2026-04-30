from pydantic import BaseModel, EmailStr, Field
from datetime import datetime


# ── Auth ────────────────────────────────────────────────────────────────────

class RegisterRequest(BaseModel):
    username: str = Field(min_length=3, max_length=30)
    email: EmailStr
    password: str = Field(min_length=6)
    password_confirm: str

class LoginRequest(BaseModel):
    username: str
    password: str

class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    username: str


# ── Library ─────────────────────────────────────────────────────────────────

class MangaItem(BaseModel):
    id: str
    title: str
    total_pages: int = 0
    cover_cloudinary_url: str = ""
    date_added: datetime

class CollectionItem(BaseModel):
    id: str
    name: str
    description: str = ""
    manga_ids: list[str] = []
    created_at: datetime

class ReadingProgressItem(BaseModel):
    manga_id: str
    current_page: int
    last_read: datetime

class ReadingHistoryItem(BaseModel):
    manga_id: str
    manga_title: str
    manga_cover_url: str = ""
    completed_at: datetime

class LibrarySyncRequest(BaseModel):
    mangas: list[MangaItem] = []
    collections: list[CollectionItem] = []
    reading_progress: list[ReadingProgressItem] = []
    reading_history: list[ReadingHistoryItem] = []
    total_usage_seconds: int = 0


# ── Public profile ───────────────────────────────────────────────────────────

class CurrentlyReadingItem(BaseModel):
    manga_title: str
    manga_cover_url: str = ""
    current_page: int = 0
    total_pages: int = 0

class PublicProfile(BaseModel):
    username: str
    bio: str = ""
    avatar_url: str = ""
    created_at: datetime
    mangas_count: int = 0
    reading_history: list[ReadingHistoryItem] = []
    total_usage_seconds: int = 0
    currently_reading: CurrentlyReadingItem | None = None
