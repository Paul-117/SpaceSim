#!/usr/bin/env python3
"""Local server for the Bridge Layout Editor.

It serves the repository only on 127.0.0.1 and exposes the current PNG files
from UI/Bridge. The editor requests that list whenever its add-element dialog
opens, so copying a new asset to the folder makes it immediately available.
"""

from __future__ import annotations

import argparse
import json
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse


class BridgeEditorHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, root: Path, **kwargs):
        self.root = root
        super().__init__(*args, directory=str(root), **kwargs)

    def do_GET(self):
        if urlparse(self.path).path == "/api/bridge-assets":
            assets = sorted(path.name for path in self.root.joinpath("UI", "Bridge").glob("*.png"))
            body = json.dumps(assets).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        super().do_GET()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--port", type=int, default=47871)
    args = parser.parse_args()
    root = args.root.resolve()
    server = ThreadingHTTPServer(
        ("127.0.0.1", args.port),
        lambda *handler_args, **handler_kwargs: BridgeEditorHandler(*handler_args, root=root, **handler_kwargs),
    )
    print(f"Bridge Layout Editor: http://127.0.0.1:{args.port}/StationPrototypes/BridgeLayoutEditor/", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
