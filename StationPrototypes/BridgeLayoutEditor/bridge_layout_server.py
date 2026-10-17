#!/usr/bin/env python3
"""Local server for the Bridge Layout Editor.

It serves the repository only on 127.0.0.1 and exposes the PNG files from a
selected UI folder. The editor requests that list whenever its add-element
dialog opens, so copying a new asset to the folder makes it immediately available.
"""

from __future__ import annotations

import argparse
import json
import subprocess
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, quote, urlparse


class BridgeEditorHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, root: Path, **kwargs):
        self.root = root
        super().__init__(*args, directory=str(root), **kwargs)

    def end_headers(self) -> None:
        self.send_header("Cache-Control", "no-store, max-age=0")
        super().end_headers()

    def send_json(self, payload: object) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def ui_folder(self, name: str) -> Path | None:
        candidate = self.root.joinpath("UI", name)
        if not name or Path(name).name != name or not candidate.is_dir():
            return None
        return candidate

    def do_GET(self):
        request = urlparse(self.path)
        if request.path == "/api/ui-folders":
            folders = sorted(path.name for path in self.root.joinpath("UI").iterdir() if path.is_dir())
            self.send_json(folders)
            return
        if request.path in {"/api/ui-assets", "/api/bridge-assets"}:
            requested = parse_qs(request.query).get("ui", ["Bridge"])[0]
            folder = self.ui_folder(requested)
            if folder is None:
                self.send_error(404, "UI folder not found")
                return
            assets = sorted(path.name for path in folder.glob("*.png"))
            reference = next((name for name in assets if name.lower() == "reference.png"), None)
            if reference is None and "Bridge UI New.png" in assets:
                reference = "Bridge UI New.png"
            self.send_json({"files": assets, "reference": reference})
            return
        super().do_GET()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--port", type=int, default=47871)
    parser.add_argument("--open", action="store_true")
    parser.add_argument("--ui", default="")
    args = parser.parse_args()
    root = args.root.resolve()
    if args.ui and (Path(args.ui).name != args.ui or not root.joinpath("UI", args.ui).is_dir()):
        raise SystemExit(f"UI folder not found: {args.ui}")
    server = ThreadingHTTPServer(
        ("127.0.0.1", args.port),
        lambda *handler_args, **handler_kwargs: BridgeEditorHandler(*handler_args, root=root, **handler_kwargs),
    )
    editor_url = f"http://127.0.0.1:{args.port}/StationPrototypes/BridgeLayoutEditor/"
    if args.ui:
        editor_url += f"?ui={quote(args.ui)}"
    print(f"Bridge Layout Editor: {editor_url}", flush=True)
    if args.open:
        try:
            subprocess.Popen(["xdg-open", editor_url], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        except FileNotFoundError:
            print(f"Open this address manually: {editor_url}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
