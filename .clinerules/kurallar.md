# AKTİF KURALLAR

Proje sıfırdan kurulur; tek hazır kaynak `CourseAssets/` (spec'ler `CourseAssets/specs/`, referans `CourseAssets/bruno/Carsties.zip`, seed `CourseAssets/snippets/seed-data.txt`). Bu dosya yalnızca asla atlanmayacak maddeleri taşır.

## Asla atlanmaz
1. **Onaysız mutating işlem yok**: `sudo`, paket kur/kaldır, container `up/down/rm`, volume silme, EF migration/drop, DB silme, dosya/klasör silme, `git push --force`. Salt-okunur inceleme onaysızdır.
2. **Uydurma yok**: emin olunmayan komut/sürüm/API davranışı `dotnet` çıktısı, `CourseAssets/specs/` veya resmi dokümanla doğrulanır.
3. **Spec tek kaynak**: `CourseAssets/specs/<servis>.md`; spec'te olmayan uç/alan eklenmez.
4. **Commit yalnız faz sonunda**, mesaj `F<n>: <kısa açıklama>`; push yalnız istenirse.
5. **Platform**: `podman-compose` (docker = emülasyon), kısa image adları alias ile, mount'ta SELinux `:Z`.
6. **Kod**: en basit çalışan çözüm (gereksiz soyutlama yok), DTO ile dışa açılım, `dotnet build` uyarısız, sırlar `dotnet user-secrets`'te.
7. **Faz sonu**: commit + kullanıcıya `/compact` hatırlatması.

Biçim: tüm görünür metin Türkçe, sonuç önce, plan ≤3 satır, komut çıktısı budanır.
