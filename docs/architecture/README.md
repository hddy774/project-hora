# 현재 제품 구조

0.2.1의 소유자와 허용 참조는 [repository_manifest.json](repository_manifest.json),
실제 entry·메모리 codec/파일 어댑터·보관 경로·Unity 후속 조건은
[v021-product-boundary.md](v021-product-boundary.md)에 기록한다.

참조 방향은 `Desktop/Android/Shell -> Hosting -> Game -> Core`다.
현재 WPF 진입점은 `EcologyWindow`이며 이전 `MainWindow`는 보관본에만 있다.

```bash
python3 scripts/validate-v021-product-boundary.py --self-test
```

Windows의 `scripts/validate-architecture.ps1`은 위 정적 검사와 현재 App.xaml 리소스
검사를 호출한다. 별도 현재 제품 빌드/기능 시나리오와 실제 네이티브 실행 검사가 필요하다.
이전 구조 설명은 [Windows 보관본](../../legacy/windows-pre-v021/docs/architecture/README.md)에 보존한다.
