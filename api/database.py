from motor.motor_asyncio import AsyncIOMotorClient, AsyncIOMotorDatabase
from api.config import MONGODB_URI, MONGODB_DB

_client: AsyncIOMotorClient | None = None

def get_db() -> AsyncIOMotorDatabase:
    assert _client is not None, "BD no inicializada"
    return _client[MONGODB_DB]

async def connect() -> None:
    global _client
    _client = AsyncIOMotorClient(MONGODB_URI)

async def disconnect() -> None:
    if _client:
        _client.close()
