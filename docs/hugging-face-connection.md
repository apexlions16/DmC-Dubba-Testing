# Hugging Face bağlantısı

## Hedefler

- Büyük video/kanıt/build dosyaları: `xykeskin/dmc-turkish-dub-qa-archive`
- Backend: Hugging Face Docker Space
- HF anahtarı yalnız backend/Space secret olarak tutulur.
- Tester ve Yönetim Merkezi EXE'lerine HF token gömülmez.
- Mevcut Hugging Face datasetlerine dokunulmaz.

## Backend ortamı

Space Variables (gizli değildir):

- `ENVIRONMENT=production`
- `STORAGE_MODE=hf`
- `HF_BUCKET_ID=xykeskin/dmc-turkish-dub-qa-archive`

Space Secrets:

- `HF_TOKEN` — bucket üzerinde gereken en dar yazma yetkisine sahip fine-grained token
- `DATABASE_URL` — kalıcı PostgreSQL bağlantısı; SQLite Space'in ephemeral diskinde production için kullanılmaz
- `DEVICE_CREDENTIAL_SECRET` — cihaz credential imzalama anahtarı
- `BOOTSTRAP_KEY` — ilk super-admin bootstrap anahtarı; bootstrap tamamlandıktan sonra kaldırılabilir

## Sağlık kontrolleri

`GET /health`

API prosesinin çalıştığını doğrular.

`GET /health/storage`

Token değerini açığa çıkarmadan şunları döndürür:

- storage mode
- bucket ID / URI
- bucket erişilebilirliği
- write credential'ın yapılandırılmış olup olmadığı
- upload için konfigürasyonun hazır olup olmadığı

`POST /admin/storage/probe`

Yalnız admin/super-admin çağırabilir. `_system/connection-probes/` altında küçük bir test nesnesi yükler ve hemen siler. Böylece gerçek upload + delete izni doğrulanır. Başarılı probe test dosyası bırakmaz.

## Space deployment

`src/api/` klasörü Space repository root'una yüklenir. İçindeki `README.md` Space'i `sdk: docker` ve `app_port: 7860` olarak tanımlar; `Dockerfile` FastAPI'yi UID 1000 altında tek Uvicorn worker ile başlatır.

Tek worker seçimi özellikle ilk sürümde DB/event sıralamasını sade tutmak içindir. Operasyonel veriler kalıcı PostgreSQL'de, medya ise HF Storage Bucket'ta tutulur.

## Güvenlik

- Public GitHub veya Space repository içine secret yazılmaz.
- HF master token masaüstü istemcilerine gönderilmez.
- Public bucket kullanıldığı için public metadata yollarında gerçek tester adı yerine immutable tester ID tercih edilir.
- Proje silme yalnız `projects/{project_id}/` namespace'i içinde çalışır.
- HF Storage Bucket non-versioned olduğu için silme/purge geri alınamaz; iki-admin onayı korunur.
