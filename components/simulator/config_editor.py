#!/usr/bin/env python3
import http.server, subprocess, os, urllib.parse

CONFIG_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'config.yaml')

HTML = """\
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Simulator Config</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{ font-family: monospace; background: #1e1e1e; color: #d4d4d4; display: flex; flex-direction: column; height: 100vh; padding: 1em; gap: .75em; }}
  h1 {{ font-size: 1em; color: #9cdcfe; }}
  form {{ display: flex; flex-direction: column; flex: 1; gap: .75em; }}
  textarea {{ flex: 1; background: #252526; color: #d4d4d4; border: 1px solid #444; padding: .75em; font-family: monospace; font-size: 13px; resize: none; }}
  button {{ padding: .5em 1.5em; background: #0e639c; color: #fff; border: none; cursor: pointer; font-size: 13px; align-self: flex-start; }}
  button:hover {{ background: #1177bb; }}
  .msg {{ font-size: 12px; color: #4ec9b0; }}
</style>
</head>
<body>
<h1>simulator/config.yaml</h1>
<form method="POST">
  <textarea name="content" spellcheck="false">{content}</textarea>
  <div style="display:flex;align-items:center;gap:1em">
    <button type="submit">Save &amp; Restart</button>
    {msg}
  </div>
</form>
</body>
</html>"""


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        content = open(CONFIG_PATH).read()
        self._respond(HTML.format(content=self._esc(content), msg=''))

    def do_POST(self):
        length = int(self.headers.get('Content-Length', 0))
        data = urllib.parse.parse_qs(self.rfile.read(length).decode())
        content = data.get('content', [''])[0]
        open(CONFIG_PATH, 'w').write(content)
        subprocess.Popen(['docker', 'restart', 'race-tracker-simulator'])
        msg = '<span class="msg">Saved — restarting simulator…</span>'
        self._respond(HTML.format(content=self._esc(content), msg=msg))

    def _esc(self, s):
        return s.replace('&', '&amp;').replace('<', '&lt;')

    def _respond(self, body):
        enc = body.encode()
        self.send_response(200)
        self.send_header('Content-Type', 'text/html; charset=utf-8')
        self.send_header('Content-Length', len(enc))
        self.end_headers()
        self.wfile.write(enc)

    def log_message(self, *_):
        pass


if __name__ == '__main__':
    server = http.server.HTTPServer(('', 5001), Handler)
    print('Config editor at http://localhost:5001', flush=True)
    server.serve_forever()
