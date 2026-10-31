"""근거 보관 전용 HTTPS 입출력. 자격 증명은 로그나 교차 호스트 redirect로 전달하지 않는다."""
import json
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

REPOSITORY = "hddy774/project-hora"
API_ROOT = "https://api.github.com/repos/" + REPOSITORY + "/"


class ArchiveError(ValueError):
    pass


def ensure(condition, message):
    if not condition:
        raise ArchiveError(message)


def https_url(url):
    try:
        parsed = urllib.parse.urlsplit(url)
        port = parsed.port
    except ValueError:
        raise ArchiveError("HTTPS 주소 형식이 잘못됐습니다") from None
    ensure(parsed.scheme == "https" and parsed.hostname and not parsed.username and not parsed.password,
           "HTTPS 주소만 허용합니다")
    ensure(port in (None, 443), "표준 HTTPS 포트만 허용합니다")
    return parsed


class NoCrossHostAuthorization(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, target):
        previous = https_url(request.full_url)
        following = https_url(target)
        redirected = super().redirect_request(request, response, code, message, headers, target)
        if redirected and previous.hostname != following.hostname:
            for collection in (redirected.headers, redirected.unredirected_hdrs):
                for name in list(collection):
                    if name.lower() == "authorization":
                        del collection[name]
        return redirected


class GitHubClient:
    def __init__(self, token, opener=None):
        ensure(bool(token), "현재 Actions GH_TOKEN이 필요합니다")
        self.token = token
        self.opener = opener or urllib.request.build_opener(NoCrossHostAuthorization())

    def _open(self, url, method="GET", payload=None, media="application/vnd.github+json", public=False,
              content_type="application/json"):
        parsed = https_url(url)
        headers = {"Accept": media, "User-Agent": "project-hora-evidence-archive",
                   "X-GitHub-Api-Version": "2022-11-28"}
        if not public:
            ensure(parsed.hostname in ("api.github.com", "uploads.github.com"), "인증 대상 호스트가 아닙니다")
            headers["Authorization"] = "Bearer " + self.token
        if payload is not None:
            headers["Content-Type"] = content_type
        request = urllib.request.Request(url, data=payload, headers=headers, method=method)
        try:
            return self.opener.open(request, timeout=90)
        except urllib.error.HTTPError as error:
            if error.code == 404:
                raise FileNotFoundError("GitHub 객체를 찾을 수 없습니다") from None
            raise ArchiveError("GitHub 요청 실패: HTTP " + str(error.code)) from None
        except (urllib.error.URLError, TimeoutError, OSError):
            raise ArchiveError("HTTPS 전송에 실패했습니다") from None

    def api(self, path, method="GET", value=None, missing=False):
        ensure(not path.startswith("/") and ":" not in path and ".." not in path, "API 경로가 잘못됐습니다")
        ensure(method == "GET" or method == "POST" and path == "releases" or
               method == "PATCH" and re.fullmatch(r"releases/[1-9][0-9]*", path),
               "허용하지 않은 변경 경로입니다. 삭제·asset 교체 API는 제공하지 않습니다")
        payload = json.dumps(value, ensure_ascii=False).encode() if value is not None else None
        try:
            with self._open(API_ROOT + path, method, payload) as response:
                data = response.read(2 * 1024 * 1024 + 1)
        except FileNotFoundError:
            if missing and method == "GET":
                return None
            raise
        ensure(len(data) <= 2 * 1024 * 1024, "API 메타데이터 크기가 한도를 넘었습니다")
        return json.loads(data)

    def download(self, url, destination, expected_bytes, public=False):
        target = Path(destination)
        target.parent.mkdir(parents=True, exist_ok=True)
        with self._open(url, media="application/octet-stream", public=public) as response:
            with target.open("wb") as stream:
                size = 0
                while chunk := response.read(65536):
                    size += len(chunk)
                    ensure(size <= expected_bytes, "다운로드 원문 크기가 고정값을 넘었습니다")
                    stream.write(chunk)
        ensure(size == expected_bytes, "다운로드 원문 크기가 고정값과 다릅니다")

    def upload(self, release_id, name, path):
        ensure(type(release_id) is int and release_id > 0, "보관 release ID가 잘못됐습니다")
        url = "https://uploads.github.com/repos/" + REPOSITORY + "/releases/" + str(release_id)
        url += "/assets?" + urllib.parse.urlencode({"name": name})
        content_type = "application/json" if name.endswith(".json") else "application/zip"
        with self._open(url, "POST", Path(path).read_bytes(), content_type=content_type) as response:
            return json.loads(response.read())
