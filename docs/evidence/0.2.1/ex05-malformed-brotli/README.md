# EX05 손상 Brotli 해석·복구 correctness 원본

정확한 수정 전 소스 `068ecefd3226d9d0530aa351fcb28c4002b9edf5`에서 기존 EX05 선택 검사는 0/1로 실패했다. 가운데 비트 `^= 0x40` 손상 fixture가 `BrotliStream.TryDecompress`의 `InvalidOperationException: Decoder ran into invalid data`를 발생시켰고, `InvalidDataException`을 처리하는 복구 경계를 우회했다. [수정 전 원본](before.original.json)을 그대로 보존한다.

수정 `5ef9d30aed82c71db56fd65df00149dae48b0023`은 시험 전용 압축 후보의 두 decoder read에서 해당 오류를 원래 inner exception을 유지한 데이터 오류로 정규화한다. 기존 손상·부분 저장·미지원 원문 검사는 유지한다. 같은 손상 압축 primary를 실제 파일로 기록해 기존 backup으로 복구하고 손상 원문을 byte-exact로 보존하는 검사도 추가했다. [정확한 소스 차이](fix-068-to-5ef.patch)와 [수정 후 같은 선택 검사 1/1 원본](after.original.json)을 보존한다. 전체 기능 통과나 제품의 압축 저장 채택 근거가 아니다.

수정 후 검사는 068 HEAD의 working patch를 빌드·실행한 뒤 그 소스 내용을 5ef로 커밋한 순서였다. 따라서 보유 DLL의 내장 revision은 **068**이며, 이를 5ef 빌드라고 표기하지 않는다. 수정 전 DLL 세트를 별도로 보존하거나 해시로 캡처하지 않았으므로 현재 DLL 해시를 수정 전 바이너리의 증명으로 사용하지 않는다. [provenance](provenance.json)는 두 원본·patch의 SHA256, 실제 명령, 보유 DLL의 revision/해시와 이 한계를 구분한다.

문서 보존의 소스 기준은 해당 수정을 이미 통합한 `56bbfb2ff3cec2df79fa28234d61a03fe4a50e63`이다. 이후에는 이 폴더의 문서·원본 복사만 추가했고 C# 소스와 보유 DLL 바이트는 변경하지 않았다. 이 보존 단계의 추가 빌드·기능 실행은 0이며, 모든 단계에서 비용·성장·장기 안정성·프로파일 측정은 0이다. 기존 두 번의 bounded correctness 실행은 실패 1회·통과 1회로 구분한다.

상위 PR #84 / 통합 #174, 관련 #179.
