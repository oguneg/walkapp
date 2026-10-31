"""Uploads the Android app bundle to Google Play and releases it to internal testing.

Uses the Play Developer API with a service account whose JSON key lives outside the repo:
~/.android-signing/play-service-account.json, or the copy next to the bottle-smash upload key. The account has
per-app access, so each game has to be added to it in Play Console (Users and permissions).

    pip install google-api-python-client google-auth
    python Tools/play_release.py                         # Builds/Android/Walkapp.aab, default notes
    python Tools/play_release.py --notes "New routes and improvements."

Internal testing has no Google review, so testers get a release within minutes. An app that has never been published
only accepts draft releases through the API; the script then saves a draft to finish in Play Console.
"""
import argparse
import pathlib

from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
from googleapiclient.http import MediaFileUpload

PACKAGE = "se.ogun.walk"
TRACK = "internal"
SIGNING = pathlib.Path.home() / ".android-signing"
KEYS = [SIGNING / "play-service-account.json", SIGNING / "bottle-smash" / "play-service-account.json"]
ROOT = pathlib.Path(__file__).resolve().parent.parent


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", default=PACKAGE)
    parser.add_argument("--aab", default=str(ROOT / "Builds" / "Android" / "Walkapp.aab"))
    parser.add_argument("--notes", default="Bug fixes and improvements.")
    args = parser.parse_args()

    credentials = service_account.Credentials.from_service_account_file(
        str(next((key for key in KEYS if key.exists()), KEYS[0])), scopes=["https://www.googleapis.com/auth/androidpublisher"])
    edits = build("androidpublisher", "v3", credentials=credentials, cache_discovery=False).edits()

    edit_id = edits.insert(packageName=args.package, body={}).execute()["id"]
    bundle = edits.bundles().upload(
        packageName=args.package, editId=edit_id,
        media_body=MediaFileUpload(args.aab, mimetype="application/octet-stream", resumable=True)).execute()
    version = bundle["versionCode"]
    print(f"Uploaded version code {version}")

    def release(status):
        edits.tracks().update(packageName=args.package, editId=edit_id, track=TRACK, body={
            "track": TRACK,
            "releases": [{
                "versionCodes": [str(version)],
                "status": status,
                "releaseNotes": [{"language": "en-GB", "text": args.notes}],
            }],
        }).execute()
        edits.commit(packageName=args.package, editId=edit_id).execute()

    try:
        release("completed")
        print(f"Released to {TRACK}.")
    except HttpError as error:
        if "draft" not in str(error).lower():
            raise
        release("draft")
        print(f"Saved as a draft on {TRACK}: this app isn't published yet, so finish the rollout in Play Console.")


if __name__ == "__main__":
    main()
