# Oyun QA Platformu

> İlk proje: **DmC: Devil May Cry Türkçe Dublaj QA**

Bu depo, oyun, mod ve özellikle seslendirme/dublaj projelerinin test süreçlerini küçük ve orta ölçekli ekiplerle düzenli, izlenebilir ve istatistiksel biçimde yönetmek için geliştirilen çoklu proje destekli bir **Game QA Management Platform** içerir.

Platformun ilk gerçek kullanım alanı **DmC: Devil May Cry Türkçe Dublaj Projesi** olacaktır. Bununla birlikte mimari hiçbir noktada tek bir oyuna veya yalnızca dublaj testine sabitlenmemiştir. Aynı sistem üzerinde eş zamanlı olarak birden fazla oyun, mod veya içerik projesi yürütülebilir.

---

## İçindekiler

- [1. Projenin amacı](#1-projenin-amacı)
- [2. Temel tasarım ilkeleri](#2-temel-tasarım-ilkeleri)
- [3. Sistemin genel mimarisi](#3-sistemin-genel-mimarisi)
- [4. Uygulamalar ve bileşenler](#4-uygulamalar-ve-bileşenler)
- [5. Kullanıcı rolleri](#5-kullanıcı-rolleri)
- [6. Tester uygulaması nasıl çalışır?](#6-tester-uygulaması-nasıl-çalışır)
- [7. Admin ve Developer uygulaması nasıl çalışır?](#7-admin-ve-developer-uygulaması-nasıl-çalışır)
- [8. Proje yapısı](#8-proje-yapısı)
- [9. Ortak görev sistemi](#9-ortak-görev-sistemi)
- [10. Build yönetimi](#10-build-yönetimi)
- [11. Hata raporlama sistemi](#11-hata-raporlama-sistemi)
- [12. Hata yaşam döngüsü](#12-hata-yaşam-döngüsü)
- [13. Retest sistemi](#13-retest-sistemi)
- [14. Bildirim ve duyuru sistemi](#14-bildirim-ve-duyuru-sistemi)
- [15. İstatistik ve analiz sistemi](#15-istatistik-ve-analiz-sistemi)
- [16. Tester istatistikleri](#16-tester-istatistikleri)
- [17. Hugging Face depolama yapısı](#17-hugging-face-depolama-yapısı)
- [18. Uygulama güncelleme sistemi](#18-uygulama-güncelleme-sistemi)
- [19. Çoklu proje desteği](#19-çoklu-proje-desteği)
- [20. Proje kapatma, arşivleme ve kalıcı silme](#20-proje-kapatma-arşivleme-ve-kalıcı-silme)
- [21. Güvenlik modeli](#21-güvenlik-modeli)
- [22. Veri modeli ve audit geçmişi](#22-veri-modeli-ve-audit-geçmişi)
- [23. Türkçe dil desteği](#23-türkçe-dil-desteği)
- [24. Kaynak kod yapısı](#24-kaynak-kod-yapısı)
- [25. Yerel geliştirme](#25-yerel-geliştirme)
- [26. CI / derleme doğrulaması](#26-ci--derleme-doğrulaması)
- [27. Mevcut geliştirme durumu](#27-mevcut-geliştirme-durumu)
- [28. Yakın dönem geliştirme planı](#28-yakın-dönem-geliştirme-planı)
- [29. Terimler](#29-terimler)

---

# 1. Projenin amacı

Bu projenin temel amacı testerların yalnızca video yüklediği basit bir dosya paylaşım uygulaması oluşturmak değildir.

Hedefimiz, bir oyun veya modun test sürecinin başlangıcından final sürümüne kadar aşağıdaki sorulara her zaman net cevap verebilen merkezi bir sistem oluşturmaktır:

- Hangi proje şu anda test ediliyor?
- Hangi build en güncel test build'i?
- Hangi tester hangi göreve atanmış durumda?
- Bir görev birden fazla tester tarafından ortak mı yürütülüyor?
- Hangi hata hangi build üzerinde bulundu?
- Hata ilk olarak kim tarafından raporlandı?
- Hatanın videosu nerede?
- Hata hangi Mission, Chapter veya bölümde oluştu?
- Hata nasıl tekrar üretilebiliyor?
- Hata hangi koşul altında ortaya çıktı?
- Admin hatayı ne zaman incelemeye aldı?
- Developer hangi build içinde bu hatayı düzeltmeyi hedefledi?
- Retest kimlerden istendi?
- Retest hangi build üzerinde yapıldı?
- İlk retest başarılı mı oldu?
- Birden fazla tester aynı retestte farklı sonuç verdi mi?
- Hata kaç kez tekrar açıldı?
- Hangi hata türlerinde daha fazla sorun çıkıyor?
- Hangi Mission daha problemli?
- Hangi build ile açık hata sayısı azaldı veya arttı?
- Ortalama çözüm süresi nedir?
- Hangi build ne zaman yüklendi, yayınlandı ve arşivlendi?
- Testerların geri bildirimleri ne kadar düzenli ve kullanılabilir?
- Proje tamamlandığında bütün dosyalar güvenli şekilde arşivlenebilir veya silinebilir mi?

Platformun bütün tasarımı bu izlenebilirlik hedefinin etrafında kurulmaktadır.

---

# 2. Temel tasarım ilkeleri

## 2.1 Tester için mümkün olan en kolay deneyim

Testerların ortalama bilgisayar kullanıcısı olduğu varsayılır.

Testerın:

- Hugging Face hesabı açması,
- token oluşturması,
- terminal kullanması,
- klasör yapısı öğrenmesi,
- GitHub kullanması,
- dosya ismi standardını ezberlemesi,
- API adresi bilmesi,
- teknik hata sınıflandırması yapması

beklenmez.

İdeal tester deneyimi şu kadar basit olmalıdır:

1. Uygulamayı aç.
2. İlk kullanımda adını yaz.
3. Sana atanmış projeyi/görevi gör.
4. Uygulama içinden güncel build'i indir.
5. Kurulum açıklamasını oku.
6. Testi yap.
7. Sorun bulursan video seç.
8. Hata türünü seç.
9. Kısa açıklama yaz.
10. **Raporu Gönder** butonuna bas.

Geri kalan bütün teknik işlemler uygulama ve backend tarafından yapılmalıdır.

## 2.2 Ölçemediğimiz şeyi yüzde olarak göstermemek

Platformda "Mission %70 test edildi" gibi sahte kesinlik üreten bir metrik kullanılmaz.

Bir oyunun gerçekten yüzde kaç test edildiğini yalnızca bulunan hata sayısından çıkarmak mümkün değildir. Yeni bir hata her an bulunabilir.

Bu nedenle progress bar yalnızca **bilinen ve geçerli sorunların çözüm yaşam döngüsünü** gösterir.

Örneğin:

- 40 geçerli sorun,
- 20 çözüldü,
- 8 üzerinde çalışılıyor,
- 5 retest bekliyor,
- 7 yeni

ise sistem "test %50 tamamlandı" demez.

Sistem:

> **Bilinen sorunların %50'si çözüldü.**

ifadesini kullanır.

## 2.3 Önemli durum değişiklikleri silinmez

Bir hata bugün `Çözüldü` durumundaysa yalnızca son durumu saklamak yeterli değildir.

Örneğin gerçek geçmiş:

```text
Yeni
↓
İncelendi
↓
Üzerinde Çalışılıyor
↓
Retest Bekliyor
↓
Retest Başarısız
↓
Üzerinde Çalışılıyor
↓
Retest Bekliyor
↓
Retest Başarılı
↓
Çözüldü
```

şeklindeyse bütün bu geçişler korunur.

Bu sayede geçmiş verilerden güvenilir istatistikler üretilebilir.

## 2.4 Hassas anahtarlar masaüstü uygulamasına gömülmez

Hugging Face write token gibi kritik bilgiler Tester veya Admin EXE içine yazılmaz.

Masaüstü uygulamaları backend ile konuşur. Hugging Face erişimi backend tarafında tutulur.

## 2.5 Runtime verisi GitHub JSON dosyalarında tutulmaz

GitHub kaynak kod, dokümantasyon, CI, release pipeline ve sürümleme içindir.

Canlı QA verileri GitHub commit geçmişine yazılmaz.

Örneğin:

- Ahmet retest yaptı,
- bir bug `In Progress` oldu,
- yeni görev oluşturuldu,
- deadline değişti,
- build indirildi

gibi olaylar veritabanında saklanır.

---

# 3. Sistemin genel mimarisi

```text
                        ┌────────────────────────────┐
                        │     Admin / Developer      │
                        │       Control Center       │
                        └─────────────┬──────────────┘
                                      │
                                      │ HTTPS / API
                                      ▼
                         ┌─────────────────────────┐
                         │      Game QA API        │
                         │        FastAPI          │
                         └──────────┬──────────────┘
                                    │
                 ┌──────────────────┼───────────────────┐
                 │                  │                   │
                 ▼                  ▼                   ▼
           PostgreSQL          Hugging Face        Bildirim /
           QA metadata         Storage Bucket      Release verisi
                 ▲                  ▲
                 │                  │
                 └────────┬─────────┘
                          │
                          │ HTTPS / API
                          ▼
                    ┌──────────────┐
                    │ Tester Client│
                    └──────┬───────┘
                           │
                           ▼
                    ┌──────────────┐
                    │ Sabit Launcher│
                    │ Auto Update   │
                    └──────────────┘
```

Temel ayrım şöyledir:

- **Veritabanı:** QA metadata ve yaşam döngüsü.
- **Hugging Face Storage Bucket:** büyük dosyalar, videolar ve build paketleri.
- **GitHub:** kaynak kod, CI ve uygulama release altyapısı.
- **Tester Client:** tester deneyimi.
- **Admin Client:** proje ve QA yönetimi.
- **Launcher:** istemci güncellemeleri.

---

# 4. Uygulamalar ve bileşenler

## Tester Client

Testerların kullandığı sade Windows masaüstü uygulamasıdır.

Ana görevleri:

- kullanıcıyı tanımak,
- atanmış projeleri göstermek,
- ortak görevleri göstermek,
- deadline takip etmek,
- en güncel test build'ini göstermek,
- build indirmek,
- kurulum açıklamasını göstermek,
- bug raporu göndermek,
- video/evidence yüklemek,
- kendisine atanmış retestleri göstermek,
- retest sonucu göndermek,
- bildirim ve zorunlu duyuruları göstermek,
- kendi gönderdiği raporların durumunu göstermek,
- proje genelindeki bilinen sorunların durumunu göstermek.

## Admin / Developer Control Center

Admin ve developerların kullandığı gelişmiş Windows uygulamasıdır.

Rol bazlı ekran gösterir.

Admin:

- proje açabilir,
- kullanıcı yönetebilir,
- görev oluşturabilir,
- deadline belirleyebilir,
- build yayınlayabilir,
- retest isteyebilir,
- bug durumlarını değiştirebilir,
- root cause girebilir,
- duyuru gönderebilir,
- analytics görebilir,
- build arşivleyebilir,
- proje kapatabilir,
- güvenli purge sürecini başlatabilir.

Developer:

- kendisine açık projeleri görebilir,
- build oluşturabilir,
- build dosyası yükleyebilir,
- changelog yazabilir,
- kurulum talimatı yazabilir,
- fix candidate bugları seçebilir,
- ilgili hata kayıtlarını inceleyebilir.

Developerın kritik yönetim yetkileri bulunmaz.

## Backend API

FastAPI tabanlı servis katmanıdır.

Masaüstü istemciler ile veritabanı/Hugging Face arasında güvenli köprü görevi görür.

## Shared Contracts

Tester ve Admin uygulamalarının aynı veri sözleşmelerini kullanmasını sağlayan ortak .NET katmanıdır.

## Launcher

Kullanıcının masaüstünde kalıcı olarak bulunan küçük başlatıcıdır.

Yeni client sürümü çıktığında payload'ı indirir, SHA-256 kontrolü yapar ve güncel client'ı çalıştırır.

---

# 5. Kullanıcı rolleri

Platform dört temel role sahiptir.

## Tester

- atanmış projeleri görür,
- build indirir,
- görev yapar,
- hata raporu gönderir,
- video gönderir,
- retest yapar,
- bildirimleri görür.

## Developer

Tester yetkilerine ek olarak:

- build metadata oluşturabilir,
- build dosyası yükleyebilir,
- changelog ve kurulum açıklaması yazabilir,
- fix candidate belirleyebilir.

## Admin

Developer yetkilerine ek olarak:

- kullanıcı yönetir,
- görev yönetir,
- build'i Current Test Build olarak yayınlar,
- bug durumunu yönetir,
- retest ister,
- bildirim gönderir,
- build arşivler,
- proje kapatır,
- analytics ekranlarının tamamını görür.

## Super Admin

Admin yetkilerine ek olarak en yüksek riskli platform işlemlerini gerçekleştirebilir.

Örneğin:

- kritik sistem ayarları,
- kalıcı proje silme süreci,
- diğer admin yetkileri.

---

# 6. Tester uygulaması nasıl çalışır?

## İlk kullanım

Tester EXE'yi ilk kez açtığında teknik bir giriş formu gösterilmez.

Kullanıcı yalnızca kendi adını yazar.

Örnek:

```text
DmC Türkçe Dublaj Test

Adınız
[ Ahmet                    ]

[ Devam ]
```

Arka planda cihaz enrollment işlemi gerçekleştirilir.

Amaç kullanıcının:

- parola,
- token,
- Hugging Face hesabı,
- API anahtarı

gibi kavramlarla uğraşmamasıdır.

## Ana ekran

Ana ekran proje odaklıdır.

Tester aynı anda birden fazla projeye atanmışsa sol menüde yalnızca kendisine atanmış projeleri görür.

Örnek:

```text
Projelerim

DmC: Devil May Cry
2 aktif görev • 1 retest

Project B
1 aktif görev
```

## Ortak görev kartı

Tester kendisine atanmış aktif ortak görevi görür.

Görev:

- başlık,
- açıklama,
- gerekli build,
- deadline,
- göreve atanmış diğer testerlar

ile birlikte gösterilir.

## Current Test Build kartı

Tester her zaman admin tarafından onaylanmış güncel test build'ini görür.

Developerın sadece yüklediği fakat adminin henüz yayınlamadığı candidate build normal tester ekranına çıkmaz.

## Retest alanı

Tester kendisinden kaç retest beklendiğini doğrudan ana ekranda görür.

## Bilinen sorunların durumu

Tester proje durumunu şu kategoriler üzerinden görür:

- Çözüldü
- Üzerinde Çalışılıyor
- Retest Bekliyor
- Yeni / Henüz Başlanmadı

Bu progress bar **test kapsamı yüzdesi değildir**.

---

# 7. Admin ve Developer uygulaması nasıl çalışır?

Admin Control Center proje seçimiyle çalışır.

Temel bölümler:

- Genel Bakış
- Hata Raporları
- Retestler
- Test Yönetimi
- Build Merkezi
- Bildirimler
- İstemci Sürümleri
- Proje Ayarları

Adminin seçtiği proje bütün ekranlar için aktif bağlam olur.

Örneğin DmC seçiliyken Project B verileri yanlışlıkla aynı tabloda gösterilmez.

## Genel Bakış

Admin aşağıdaki değerleri anlık görebilir:

- toplam rapor,
- geçerli sorun,
- aktif sorun,
- çözülmüş sorun,
- retest bekleyen sorun,
- çözüm oranı,
- bug türleri,
- tester geri bildirim istatistikleri,
- retest sağlığı.

## Hata Raporları

Filtreler:

- durum,
- bug türü,
- build,
- tester,
- Mission/Chapter,
- trigger,
- tarih,
- reproducibility,
- retest sonucu.

Amaç yüzlerce rapor içinde istenilen probleme birkaç saniyede ulaşabilmektir.

---

# 8. Proje yapısı

Platformun en üst nesnesi `Project`tir.

Örnek:

```text
QA Platform
│
├── DmC: Devil May Cry Türkçe Dublaj
├── Project B
├── Project C
└── Project D
```

Her proje kendi:

- üyelerine,
- bölümlerine,
- buildlerine,
- görevlerine,
- buglarına,
- retestlerine,
- bildirimlerine,
- analytics verilerine

sahiptir.

Projeler birbirinden veri seviyesinde ayrılır.

## Section

Her oyunda aynı terim kullanılmak zorunda değildir.

Örneğin:

- DmC → Mission
- başka oyun → Chapter
- başka oyun → Episode
- başka oyun → Level

Proje oluşturulurken section etiketi seçilebilir.

---

# 9. Ortak görev sistemi

Bir admin bir görevi bir veya birden fazla testera atayabilir.

Örneğin:

```text
Görev: Mission 04 Tam Bölüm Testi
Build: 0.6.4
Deadline: 14 gün

Atananlar:
- Ahmet
- Mehmet
- Burak
```

Sistem üç farklı görev oluşturmaz.

Tek bir shared task oluşturur.

Üç tester da aynı ortak görev panelini görür.

Ortak panelde:

- görev başlığı,
- açıklama,
- build,
- deadline,
- atanan kişiler,
- görev kapsamında raporlanan sorunlar,
- son aktiviteler

ortaktır.

Kişisel alanlar ise kişiye göre değişebilir:

- benim raporlarım,
- benden beklenen retestler,
- benim bildirimlerim.

---

# 10. Build yönetimi

Build sistemi platformun ana parçalarından biridir.

## Build durumları

Temel lifecycle:

```text
Uploading
↓
Candidate
↓
Current
↓
Superseded
↓
Archived
```

Türkçe arayüzde bu durumlar Türkçeleştirilmiş karşılıklarıyla gösterilir.

## Developer build yükleme

Developer yeni build oluştururken şunları girer:

- sürüm,
- başlık,
- açıklama,
- changelog,
- kurulum talimatı,
- build dosyası,
- bu buildde düzeltilmesi hedeflenen buglar.

Örnek:

```text
Sürüm: 0.6.4
Başlık: Mission 04 Ses Düzeltmeleri

Değişiklikler:
- Dante checkpoint tekrar problemi düzeltildi.
- Boss intro senkron ayarı güncellendi.

Kurulum:
1. Eski mod klasörünü kaldırın.
2. ZIP içeriğini oyun klasörüne çıkartın.
3. Oyunu yeniden başlatın.
```

## Candidate ve Current farkı

Developerın yüklediği son dosya otomatik olarak bütün testerlara dağıtılmaz.

Bu güvenlik açısından özellikle önemlidir.

Admin build'i kontrol ettikten sonra:

> **Güncel Test Build'i Olarak Yayınla**

işlemini gerçekleştirir.

Bu işlemden sonra tester uygulaması yeni build'i en güncel build olarak gösterir.

## Build ile görev bağlantısı

Bir shared task belirli bir build'e bağlanabilir.

Bu nedenle yeni build çıktığında eski görev sessizce başka build'e geçirilmez.

Admin karar verir:

- görev eski build ile devam etsin,
- görev yeni build'e geçirilsin,
- yeni build için yeni görev oluşturulsun.

## Build arşivleme

Build test süreci bittikten sonra yalnızca admin tarafından arşivlenebilir.

Arşivlenecek build:

- Current olamaz,
- açık bir görev tarafından zorunlu build olarak kullanılıyor olamaz.

Arşivlemede büyük dosya mümkün olduğunca tekrar upload edilmez. Hugging Face bucket içinde aktif prefix'ten archived prefix'e taşınır/kopyalanır.

---

# 11. Hata raporlama sistemi

Tester hata gönderirken mümkün olduğunca structured veri üretir.

Örnek alanlar:

- Proje
- Mission / Chapter
- Görev
- Test edilen build
- Başlık
- Hata türü
- Hatanın oluştuğu koşul
- Açıklama
- Reproducibility
- Video timestamp
- Video / evidence

## Hata türleri

DmC Türkçe Dublaj projesinde başlangıç listesi:

- Eksik Türkçe Ses
- Yanlış Replik / Yanlış Ses
- Senkron Problemi
- Ses Seviyesi / Mix Problemi
- Ses Kalitesi Problemi
- Kesilen Replik
- Tekrarlanan Replik
- Teknik Problem
- Diğer

Bu liste ileride proje bazlı özelleştirilebilir.

## Trigger / oluşma koşulu

Örnek:

- Normal oynanış
- Ara sahne atlama
- Ölüm / Checkpoint
- Mission yeniden başlatma
- Dövüşü hızlı bitirme
- Dövüşü uzun sürdürme
- Boss geçişi
- Bilinmiyor

Tester teknik root cause seçmek zorunda değildir.

Tester gördüğü koşulu bildirir.

Teknik root cause admin/developer incelemesinde belirlenir.

---

# 12. Hata yaşam döngüsü

Ana akış:

```text
Yeni
↓
İncelendi
↓
Üzerinde Çalışılıyor
↓
Retest Bekliyor
↓
Çözüldü
```

Yan durumlar:

- Beklemede
- Tekrar Rapor
- Bug Değil
- Düzeltilmeyecek
- Yeniden Açıldı

## Geçmişin korunması

Bir bugın status alanı değiştirildiğinde önceki olay kaybolmaz.

Her değişiklik event olarak kaydedilir.

Örneğin:

```text
10 Ağustos 14:31 — Ahmet rapor oluşturdu
10 Ağustos 15:06 — Hasan raporu inceledi
11 Ağustos 09:22 — Üzerinde çalışılıyor
13 Ağustos 18:14 — Build 0.6.2 için retest istendi
14 Ağustos 11:42 — Retest başarısız
15 Ağustos 10:17 — Tekrar çalışmaya alındı
17 Ağustos 16:21 — Build 0.6.3 için retest istendi
18 Ağustos 13:04 — Retest başarılı
18 Ağustos 13:32 — Admin tarafından çözüldü
```

---

# 13. Retest sistemi

Retest platformda ayrı ve birinci sınıf bir nesnedir.

Admin bir bug için retest oluşturduğunda:

- hangi build üzerinde yapılacağı,
- kimlerden istendiği,
- deadline,
- admin notu

kaydedilir.

Retest:

- ilk raporlayan tester,
- göreve atanmış bütün testerlar,
- admin tarafından seçilen belirli testerlar

için oluşturulabilir.

## Tester sonucu

Tester üç temel sonuçtan birini seçebilir:

- Sorun artık oluşmuyor
- Sorun hâlâ oluşuyor
- Emin olamadım

Tester gerektiğinde yeni video ekleyebilir.

## Birden fazla tester sonucu

Örnek:

```text
Ahmet   ✅ Başarılı
Mehmet  ✅ Başarılı
Burak   ❌ Başarısız
```

Bu durumda bug otomatik olarak çözüldü sayılmaz.

Admin bütün sonuçları birlikte değerlendirir.

## Güvenli kapanış

Başarılı retest bir bugı otomatik olarak `Çözüldü` yapmaz.

Final çözüm kararı admine aittir.

Başarısız retest ise bugı tekrar çalışma sürecine döndürebilir.

---

# 14. Bildirim ve duyuru sistemi

Admin uygulama içinden tester veya tester gruplarına bildirim gönderebilir.

Hedefler:

- herkes,
- belirli kullanıcı,
- seçili kullanıcılar,
- belirli projedeki kişiler,
- belirli shared task üyeleri.

## Normal bildirim

Örnek:

> Mission 04 teste açıldı.

## Kritik bildirim

Örnek:

> Mission 06 buildinde bozuk dosya tespit edildi. Yeni build gelene kadar test yapmayın.

## Okunması zorunlu duyuru

`MUST READ` niteliğindeki duyuru tester tarafından onaylanmadan uygulamanın normal akışına devam edilmemesi planlanmaktadır.

Admin şu bilgiyi görebilir:

```text
Ahmet   Okudu
Mehmet  Okudu
Burak   Okumadı
```

---

# 15. İstatistik ve analiz sistemi

Platformun temel hedeflerinden biri QA verisini son derece görünür hale getirmektir.

Dashboard yalnızca dekoratif grafikler üretmemelidir.

Bir sayı veya grafik segmentine tıklandığında mümkün olduğunca arkasındaki gerçek bug kayıtlarına gidilebilmelidir.

## Ana metrikler

- toplam rapor,
- geçerli bug,
- aktif bug,
- çözülen bug,
- retest bekleyen bug,
- duplicate,
- not a bug,
- reopened / regression,
- çözüm oranı.

## Bug türü istatistikleri

Örnek:

```text
Eksik Türkçe Ses       52
Senkron                 47
Mix                     38
Kesilen Replik          31
Yanlış Replik           22
```

Her kategori için ayrıca:

- çözüm oranı,
- ortalama çözüm süresi,
- retest başarı oranı,
- reopen oranı

hesaplanabilir.

## Bölüm bazlı istatistikler

Örnek:

```text
Mission 01   32 bug   30 çözüldü
Mission 02   41 bug   35 çözüldü
Mission 03   67 bug   31 çözüldü
```

## Build karşılaştırması

Örnek:

```text
Build      Açık Bug   Yeni Bug   Çözülen   Regression
0.6.2         78         31         18          8
0.6.3         64         21         35          3
0.6.4         43         12         33          2
0.6.5         19          5         29          0
```

## Retest analytics

- toplam retest,
- ilk retestte başarılı,
- ilk retestte başarısız,
- ikinci retest gereken,
- üç veya daha fazla retest gereken,
- ortalama retest süresi.

## Süre metrikleri

Event geçmişinden şu metrikler çıkarılabilir:

- rapor → ilk admin incelemesi,
- doğrulama → çalışmaya başlama,
- çalışma → retest,
- ilk rapor → çözüm.

---

# 16. Tester istatistikleri

Platform bir "en çok bug bulan tester" yarışına dönüştürülmez.

Bu tür leaderboardlar gereksiz veya kalitesiz raporu teşvik edebilir.

Bunun yerine tester profili için sağlıklı metrikler tutulur:

- gönderilen rapor sayısı,
- doğrulanmış bug sayısı,
- duplicate sayısı,
- not a bug sayısı,
- video ekleme oranı,
- reproduction bilgisi ekleme oranı,
- retest talepleri,
- tamamlanan retestler,
- retest cevap oranı,
- ağırlıklı olarak bulduğu bug kategorileri.

Bu bilgiler görev dağılımını iyileştirmek için kullanılabilir.

---

# 17. Hugging Face depolama yapısı

Büyük dosyalar için Hugging Face Storage Bucket kullanılmaktadır.

İlk bucket:

```text
xykeskin/dmc-turkish-dub-qa-archive
```

Mevcut başka Hugging Face datasetleri bu sistem tarafından kullanılmaz veya değiştirilmez.

## Önerilen proje prefix yapısı

```text
projects/
  project_dmc_001/
    reports/
      DMC-000001/
        evidence/
          video.mp4

    retests/
      RETEST-000001/
        evidence/
          ahmet-video.mp4

    builds/
      active/
        build-00042/
          DmC-Dub-0.6.4.zip

      archived/
        build-00041/
          DmC-Dub-0.6.3.zip
```

Bucket klasörleri/prefixleri kullanıcı tarafından elle oluşturulmak zorunda değildir.

Dosya ilgili remote path'e yazıldığında yapı doğal olarak oluşur.

## Metadata neden bucketta tutulmuyor?

QA state'in ana kaynağı veritabanıdır.

Bucket büyük binary dosyaların depolama katmanıdır.

---

# 18. Uygulama güncelleme sistemi

Testerın her sürümde yeni bir EXE indirmesi hedeflenmez.

Kullanıcıya bir kez kalıcı launcher verilir.

Örnek:

```text
DmC-Dub-QA.exe
```

Launcher:

1. backendden güncel release manifestini ister,
2. local sürüm ile karşılaştırır,
3. yeni sürüm varsa paketini indirir,
4. SHA-256 doğrulaması yapar,
5. yeni sürümü ayrı klasöre çıkarır,
6. client'ı çalıştırır,
7. güncelleme başarısızsa uygun durumda önceki çalışan sürüme geri dönebilir.

## Stable ve Beta

İki release kanalı planlanmıştır:

- Stable
- Beta

Normal testerlar Stable kanalını kullanır.

Admin/developer ekibi yeni özellikleri Beta kanalında deneyebilir.

## Güncelleme notları

Her client release için:

- sürüm,
- başlık,
- güncelleme notu,
- yayın tarihi

saklanır.

Tester yeni sürümü ilk kez açtığında release notes gösterilebilir.

---

# 19. Çoklu proje desteği

Sistem DmC bittikten sonra çöpe atılacak tek oyunluk bir araç değildir.

Aynı anda örneğin:

```text
DmC Türkçe Dublaj        10 tester
Project B                 5 tester
Project C                 7 tester
Project D                 3 tester
```

çalışabilir.

Bir tester birden fazla projede olabilir.

Başka projeye atanmamış kullanıcı o projeyi görmez.

Project scope şu nesnelere uygulanır:

- üyelik,
- section,
- task,
- build,
- bug,
- retest,
- notification,
- analytics,
- storage path.

---

# 20. Proje kapatma, arşivleme ve kalıcı silme

Proje tamamlandığında iki farklı kavram vardır.

## Projeyi Kapat

Güvenli ve geri döndürülebilir operasyondur.

```text
ACTIVE
↓
CLOSED
```

Kapatılan projede:

- yeni normal test görevi açılamaz,
- testerların aktif görünümünden çıkarılabilir,
- mevcut kayıtlar admin tarafından incelenebilir,
- dosyalar silinmez.

## Permanent Purge

Projenin verisini gerçekten kaldırır.

Bu işlem yanlışlıkla yapılamayacak şekilde tasarlanacaktır.

Hedef güvenlik adımları:

1. Proje önce `CLOSED` olmalı.
2. Silinecek veri özeti gösterilmeli.
3. Proje adı elle yazılmalı.
4. Rastgele doğrulama kodu girilmeli.
5. İkinci farklı admin onayı alınmalı.
6. Yalnızca ilgili `project_id` namespace'i silinmeli.

Örnek özet:

```text
Silinecek:
142 bug raporu
197 video
38 retest videosu
24 görev
187 bildirim kaydı
128.7 GB Hugging Face verisi
```

## Audit tombstone

Tercih edilirse içerik tamamen silindikten sonra yalnızca şu tip minimum audit kaydı korunabilir:

- proje ID,
- oluşturulma tarihi,
- kapanma tarihi,
- purge tarihi,
- purge onaylayan adminler,
- silinen toplam dosya/veri miktarı.

Bu kayıt proje içeriğini içermez.

---

# 21. Güvenlik modeli

## HF token istemciye verilmez

Tester ve Admin uygulamasına Hugging Face write token gömülmez.

## Cihaz enrollment

Tester ilk kullanımda adını yazsa da arkada cihaz eşleştirmesi yapılması planlanmaktadır.

Amaç EXE başka bilgisayara kopyalandığında aynı tester adının otomatik olarak yeni cihazda kabul edilmemesidir.

Cihaz sıfırlama admin tarafından yapılabilir.

## Rol bazlı yetkilendirme

Backend yalnızca UI gizlemeye güvenmez.

Yetki kontrolü API seviyesinde de yapılır.

Örneğin developer arayüzde Archive butonunu görmese bile API üzerinden archive işlemi yapmasına izin verilmemelidir.

## Proje kapsamı kontrolü

Bir kullanıcı Project A üyesiyse Project B kaynaklarına yalnızca ID tahmin ederek ulaşamamalıdır.

## Kritik işlemler

Permanent purge gibi işlemler standart admin aksiyonlarından ayrı güvenlik katmanlarına sahip olmalıdır.

---

# 22. Veri modeli ve audit geçmişi

Temel tablolar / domain nesneleri:

```text
Users
Devices
Projects
ProjectMembers
Sections
Builds
BuildFixCandidates
Tasks
TaskAssignees
BugReports
BugEvents
EvidenceAssets
RetestRequests
RetestAssignees
RetestResults
Notifications
NotificationRecipients
ClientReleases
AuditEvents
PurgeRequests
```

## Neden BugEvent var?

`BugReport.status = resolved` tek başına yeterli bilgi değildir.

BugEvent sayesinde bugın bütün yaşam öyküsü korunur.

Bu sistem analytics için temel veri kaynağıdır.

## AuditEvent

Build upload, download, archive veya kritik admin işlemleri gibi bug dışındaki olaylar için genel audit event tutulabilir.

---

# 23. Türkçe dil desteği

Platformun ana kullanıcı dili **Türkçe**dir.

Tester ve Admin/Developer masaüstü uygulamalarında kullanıcıya görünen:

- pencere başlıkları,
- menüler,
- butonlar,
- durum adları,
- hata mesajları,
- bildirimler,
- boş durum mesajları,
- update ekranları,
- progress açıklamaları,
- tarih/süre metinleri

Türkçe gösterilmelidir.

Kod seviyesindeki enum ve API alanlarının İngilizce olması teknik olarak sorun değildir. UI katmanı bunları Türkçe karşılıklarıyla göstermelidir.

Örneğin:

```text
in_progress       → Üzerinde Çalışılıyor
retest_required   → Retest Bekliyor
resolved          → Çözüldü
duplicate         → Tekrar Rapor
not_a_bug         → Bug Değil
wont_fix          → Düzeltilmeyecek
reopened          → Yeniden Açıldı
```

Türkçe kullanıcı deneyimi yalnızca bazı butonların çevrilmesi olarak değerlendirilmemelidir. Son kullanıcıya çıkan bütün uygulama akışı Türkçe olmalıdır.

---

# 24. Kaynak kod yapısı

```text
src/
├── api/
│   └── app/
│       ├── application.py
│       ├── main.py
│       ├── models.py
│       ├── schemas.py
│       ├── services.py
│       ├── storage.py
│       ├── build_api.py
│       ├── release_api.py
│       └── auth.py
│
├── Shared/
│   ├── Contracts.cs
│   └── ApiClient.cs
│
├── TesterApp/
│   ├── App.xaml
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
│
├── AdminApp/
│   ├── App.xaml
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
│
└── Launcher/
    ├── Launcher.csproj
    └── Program.cs
```

Dokümantasyon:

```text
docs/
├── architecture.md
└── domain-model.md
```

CI:

```text
.github/
└── workflows/
    └── ci.yml
```

---

# 25. Yerel geliştirme

> Bu bölüm geliştirme ortamı içindir. Normal testerların bunların hiçbirini yapması beklenmez.

## Backend

Gereksinimler:

- Python 3.13
- pip

Örnek kurulum:

```bash
cd src/api
pip install -e '.[dev]'
```

Geliştirme veritabanı varsayılan olarak SQLite ile çalışabilecek şekilde tasarlanmıştır.

Production için hedef PostgreSQL'dir.

Önemli environment değerleri:

```text
DATABASE_URL
HF_BUCKET_ID
HF_TOKEN
BOOTSTRAP_KEY
DEVICE_CREDENTIAL_SECRET
```

`HF_TOKEN` kaynak koda yazılmamalıdır.

## Windows uygulamaları

Gereksinim:

- .NET 8 SDK

Derleme:

```powershell
dotnet build src/Shared/Shared.csproj -c Release
dotnet build src/TesterApp/TesterApp.csproj -c Release
dotnet build src/AdminApp/AdminApp.csproj -c Release
dotnet build src/Launcher/Launcher.csproj -c Release
```

---

# 26. CI / derleme doğrulaması

GitHub Actions CI iki ana job çalıştırır.

## Backend

- bağımlılık kurulumu,
- Python compile kontrolü,
- FastAPI application import kontrolü,
- kritik correctness lint kontrolü.

## Windows istemcileri

Gerçek Windows runner üzerinde:

- Shared,
- TesterApp,
- AdminApp,
- Launcher

derlenir.

Amaç yalnızca kaynak kodun repositorye yazılmış olması değil, Windows üzerinde gerçekten compile edilebilir olmasıdır.

---

# 27. Mevcut geliştirme durumu

Aşağıdaki tablo README güncellendiği andaki genel durumu anlatır.

| Bileşen | Durum |
|---|---|
| Çoklu proje domain modeli | Temel hazır |
| User / rol modeli | Temel hazır |
| Build metadata modeli | Temel hazır |
| Shared task modeli | Temel hazır |
| Bug lifecycle | Temel hazır |
| Bug event history | Temel hazır |
| Retest modeli | Temel hazır |
| Analytics summary temeli | Temel hazır |
| Notification veri modeli | Temel hazır |
| Release veri modeli | Temel hazır |
| Purge veri modeli | Temel hazır |
| HF Storage Bucket adapter | Temel hazır |
| Build upload/download/archive API | Temel hazır |
| Tester WPF shell | Derleniyor / temel UI hazır |
| Admin WPF shell | Derleniyor / temel UI hazır |
| Launcher | Derleniyor / update temeli hazır |
| Device credential helper | Temel hazır |
| Device enrollment uçtan uca | Geliştirilecek |
| Video evidence upload UI/API | Geliştirilecek |
| Retest gerçek UI/API bağlantısı | Geliştirilecek |
| WebSocket anlık bildirim | Geliştirilecek |
| Derin analytics drill-down | Geliştirilecek |
| Build Center tam masaüstü akışı | Geliştirilecek |
| Client release yayın pipeline | Geliştirilecek |
| PostgreSQL migration / Alembic | Geliştirilecek |
| İki admin onaylı purge executor | Geliştirilecek |
| Production Hugging Face Space deployment | Geliştirilecek |

---

# 28. Yakın dönem geliştirme planı

Öncelik sırası:

1. Tam Türkçe UI ve merkezi durum/metin çeviri katmanı.
2. Device enrollment + güvenli cihaz credential akışı.
3. Tester bug rapor formu.
4. Video evidence upload.
5. Video timestamp işaretleme.
6. Admin bug detail/video player.
7. Shared task gerçek API bağlantısı.
8. Retest request ve tester retest akışı.
9. Build Center tam upload/download/installation akışı.
10. Bildirim ve WebSocket sistemi.
11. MUST READ duyuru akışı.
12. Analytics drill-down endpointleri.
13. Tester analytics.
14. Build analytics ve karşılaştırma.
15. Release publishing pipeline.
16. PostgreSQL/Alembic migration.
17. Güvenli project close/purge executor.
18. Production deployment.

---

# 29. Terimler

## Project

Sistem üzerinde yönetilen oyun/mod/test projesi.

## Section

Mission, Chapter, Episode veya Level gibi proje içi bölüm.

## Build

Testerların üzerinde test yaptığı oyun/mod sürümü.

## Candidate Build

Developer tarafından yüklenmiş fakat henüz admin tarafından genel test için onaylanmamış build.

## Current Test Build

Adminin testerların kullanması için aktif olarak yayınladığı build.

## Shared Task

Aynı test görevinin birden fazla tester tarafından ortak yürütülen tek kaydı.

## Bug Report

Tester tarafından oluşturulmuş hata kaydı.

## Evidence

Bug veya retesti destekleyen video/görsel dosya.

## Retest

Bir düzeltmenin belirli build üzerinde tekrar kontrol edilmesi.

## Fix Candidate

Developerın belirli bir buildde düzeltildiğini düşündüğü ve retest edilmesini hedeflediği bug.

## Reopened / Regression

Daha önce çözüldüğü düşünülen bir sorunun tekrar görülmesi.

## Audit Event

Sistemde gerçekleşmiş önemli bir işlemin tarihsel kaydı.

## Purge

Bir projenin dosya ve runtime verilerinin güvenlik kontrollerinden sonra kalıcı olarak kaldırılması.

---

## Son not

Bu platformun başarısı yalnızca çok fazla özellik sunmasına bağlı değildir.

Asıl hedef:

- tester için kolay,
- admin için görünür,
- developer için kullanışlı,
- veri açısından izlenebilir,
- güvenlik açısından kontrollü,
- proje bittikten sonra yönetilebilir

bir QA süreci oluşturmaktır.

DmC: Devil May Cry Türkçe Dublaj projesi bu sistemin ilk gerçek kullanım alanıdır; ancak altyapının uzun vadeli amacı birden fazla oyun ve mod projesinin QA süreçlerini aynı uygulama üzerinden düzenli biçimde yönetebilmektir.
