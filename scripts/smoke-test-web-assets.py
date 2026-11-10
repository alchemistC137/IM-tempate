import sys
from html.parser import HTMLParser
from urllib.parse import urljoin, urlsplit
from urllib.request import Request, urlopen


class PageAssets(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "script" and attrs.get("src"):
            self.assets.append((attrs["src"], "javascript"))
        elif tag == "link" and attrs.get("rel") == "stylesheet" and attrs.get("href"):
            self.assets.append((attrs["href"], "css"))


def fetch(url):
    request = Request(url, headers={"X-Forwarded-Proto": "https"})
    with urlopen(request, timeout=15) as response:
        if response.status != 200:
            raise RuntimeError(f"{url}: expected HTTP 200, got {response.status}")
        return response.headers.get_content_type(), response.read()


def main(base_url):
    content_type, html = fetch(base_url)
    if content_type != "text/html":
        raise RuntimeError(f"{base_url}: expected HTML, got {content_type}")

    page = PageAssets()
    page.feed(html.decode("utf-8"))
    if not any("_framework/blazor.web" in path for path, _ in page.assets):
        raise RuntimeError("The page does not reference the Blazor boot script")

    for path, kind in page.assets:
        url = urljoin(base_url, path)
        if urlsplit(url).netloc != urlsplit(base_url).netloc:
            raise RuntimeError(f"Unexpected external asset: {url}")
        content_type, content = fetch(url)
        allowed_types = {"text/css"} if kind == "css" else {
            "text/javascript", "application/javascript"
        }
        if content_type not in allowed_types or not content:
            raise RuntimeError(
                f"{url}: expected non-empty {kind}, got {content_type}, {len(content)} bytes"
            )
        print(f"OK: {path} ({len(content)} bytes)")


if __name__ == "__main__":
    main(sys.argv[1])
