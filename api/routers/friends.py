from fastapi import APIRouter, Depends, HTTPException, status
from datetime import datetime, timezone
from api.database import get_db
from api.deps import get_current_user

router = APIRouter(prefix="/friends", tags=["friends"])


@router.get("")
async def list_friends(me: str = Depends(get_current_user)):
    db = get_db()
    docs = await db.friendships.find(
        {"$or": [{"requester": me}, {"recipient": me}], "status": "accepted"}
    ).to_list(200)
    usernames = [d["recipient"] if d["requester"] == me else d["requester"] for d in docs]
    user_docs  = await db.users.find({"username": {"$in": usernames}}).to_list(200)
    avatar_map = {u["username"]: u.get("avatar_url", "") for u in user_docs}
    return [{"username": u, "avatar_url": avatar_map.get(u, "")} for u in usernames]


@router.get("/requests")
async def pending_requests(me: str = Depends(get_current_user)):
    db = get_db()
    docs       = await db.friendships.find({"recipient": me, "status": "pending"}).to_list(100)
    requesters = [d["requester"] for d in docs]
    user_docs  = await db.users.find({"username": {"$in": requesters}}).to_list(100)
    avatar_map = {u["username"]: u.get("avatar_url", "") for u in user_docs}
    return [
        {"from": d["requester"], "id": str(d["_id"]), "avatar_url": avatar_map.get(d["requester"], "")}
        for d in docs
    ]


@router.post("/{username}/request", status_code=201)
async def send_request(username: str, me: str = Depends(get_current_user)):
    if username == me:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "No puedes añadirte a ti mismo")

    db = get_db()
    if not await db.users.find_one({"username": username}):
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Usuario no encontrado")

    exists = await db.friendships.find_one({
        "$or": [
            {"requester": me, "recipient": username},
            {"requester": username, "recipient": me},
        ]
    })
    if exists:
        raise HTTPException(status.HTTP_409_CONFLICT, "Ya existe una solicitud o amistad")

    await db.friendships.insert_one({
        "requester":  me,
        "recipient":  username,
        "status":     "pending",
        "created_at": datetime.now(timezone.utc),
    })


@router.put("/{username}/accept", status_code=204)
async def accept_request(username: str, me: str = Depends(get_current_user)):
    db = get_db()
    result = await db.friendships.update_one(
        {"requester": username, "recipient": me, "status": "pending"},
        {"$set": {"status": "accepted"}},
    )
    if result.matched_count == 0:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Solicitud no encontrada")


@router.delete("/{username}/request", status_code=204)
async def reject_request(username: str, me: str = Depends(get_current_user)):
    db = get_db()
    await db.friendships.delete_one(
        {"requester": username, "recipient": me, "status": "pending"}
    )


@router.delete("/{username}", status_code=204)
async def remove_friend(username: str, me: str = Depends(get_current_user)):
    db = get_db()
    await db.friendships.delete_one({
        "$or": [
            {"requester": me, "recipient": username},
            {"requester": username, "recipient": me},
        ],
        "status": "accepted",
    })
