# Oyun QA Platformu

> İlk gerçek kullanım alanı: **DmC: Devil May Cry Türkçe Dublaj QA**

Bu depo; oyun, mod, dublaj, yerelleştirme ve benzeri projelerin test süreçlerini küçük ve orta ölçekli ekiplerle düzenli, güvenli, ölçülebilir ve izlenebilir biçimde yönetmek için geliştirilen çoklu proje destekli bir **Oyun Kalite Güvence Yönetim Platformu** içerir.

Platformun ilk kullanım alanı **DmC: Devil May Cry Türkçe Dublaj Projesi** olacaktır. Buna rağmen mimari tek bir oyuna, yalnızca dublaja veya yalnızca bir test ekibine sabitlenmemiştir. Aynı sistem üzerinde aynı anda birden fazla oyun, mod veya farklı QA projesi yürütülebilir.

Bu README yalnızca geliştiriciler için teknik kurulum notu değildir. Uygulamanın neden var olduğunu, testerın ne göreceğini, yöneticinin ne yapacağını, buildlerin nasıl dağıtılacağını, hataların nasıl yaşayacağını, yeniden testlerin nasıl yönetileceğini, istatistiklerin ne anlama geldiğini ve bir proje tamamlandığında verilerin nasıl ele alınacağını açıklayan ana proje kılavuzudur.

---

## İçindekiler

- [1. Projenin amacı](#1-projenin-amacı)
- [2. Tasarım ilkeleri](#2-tasarım-ilkeleri)
- [3. Genel mimari](#3-genel-mimari)
- [4. Ana bileşenler](#4-ana-bileşenler)
- [5. Kullanıcı rolleri](#5-kullanıcı-rolleri)
- [6. Tester uygulaması](#6-tester-uygulaması)
- [7. Yönetim Merkezi](#7-yönetim-merkezi)
- [8. Güvenli cihaz eşleştirmesi](#8-güvenli-cihaz-eşleştirmesi)
- [9. Proje yapısı ve çoklu proje desteği](#9-proje-yapısı-ve-çoklu-proje-desteği)
- [10. Ortak görev sistemi](#10-ortak-görev-sistemi)
- [11. Son tarih sistemi](#11-son-tarih-sistemi)
- [12. Test sürümü / build yönetimi](#12-test-sürümü--build-yönetimi)
- [13. Build indirme ve kurulum açıklamaları](#13-build-indirme-ve-kurulum-açıklamaları)
- [14. Hata raporlama](#14-hata-raporlama)
- [15. Video ve kanıt dosyaları](#15-video-ve-kanıt-dosyaları)
- [16. Hata yaşam döngüsü](#16-hata-yaşam-döngüsü)
- [17. Yeniden test sistemi](#17-yeniden-test-sistemi)
- [18. Bildirim ve duyurular](#18-bildirim-ve-duyurular)
- [19. İstatistik ve analiz yaklaşımı](#19-istatistik-ve-analiz-yaklaşımı)
- [20. Tester istatistikleri](#20-tester-istatistikleri)
- [21. Build istatistikleri](#21-build-istatistikleri)
- [22. Hugging Face depolama düzeni](#22-hugging-face-depolama-düzeni)
- [23. Uygulama güncelleme sistemi](#23-uygulama-güncelleme-sistemi)
- [24. Türkçe dil desteği](#24-türkçe-dil-desteği)
- [25. Güvenlik modeli](#25-güvenlik-modeli)
- [26. Veritabanı ve olay geçmişi](#26-veritabanı-ve-olay-geçmişi)
- [27. Proje kapatma, arşivleme ve kalıcı silme](#27-proje-kapatma-arşivleme-ve-kalıcı-silme)
- [28. Kaynak kod yapısı](#28-kaynak-kod-yapısı)
- [29. Yerel geliştirme](#29-yerel-geliştirme)
- [30. CI ve derleme doğrulaması](#30-ci-ve-derleme-doğrulaması)
- [31. Mevcut geliştirme durumu](#31-mevcut-geliştirme-durumu)
- [32. Yakın dönem geliştirme planı](#32-yakın-dönem-geliştirme-planı)
- [33. Terimler sözlüğü](#33-terimler-sözlüğü)

---

# 1. Projenin amacı

Bu projenin amacı yalnızca testerların video yükleyebileceği bir masaüstü aracı yapmak değildir.

Hedef; test sürecinin tamamını baştan sona izleyebilen merkezi bir QA sistemi oluşturmaktır.

Sistem aşağıdaki soruların cevaplarını mümkün olduğunca tek bir yerden verebilmelidir:

- Şu anda hangi projeler aktif?
- Her projede hangi test sürümü güncel?
- Güncel test sürümünü kim yükledi?
- Hangi sürüm ne zaman yayınlandı?
- Testerlar hangi sürümü indirdi?
- Hangi tester hangi göreve atanmış?
- Aynı görev üzerinde kimler ortak çalışıyor?
- Görevin son tarihi ne?
- Hangi hata hangi build üzerinde bulundu?
- Hata hangi Mission, Chapter veya bölümde oluştu?
- Hata hangi koşul altında ortaya çıktı?
- Hata ne kadar tekrar üretilebilir?
- Hatanın video kaydı var mı?
- Videoda hata hangi saniyede oluşuyor?
- İlk raporu kim gönderdi?
- Admin raporu ne zaman inceledi?
- Hata ne zaman çalışmaya alındı?
- Developer hangi buildde düzeltmeyi hedefledi?
- Hangi testerlardan yeniden test istendi?
- Yeniden test hangi buildde yapıldı?
- İlk yeniden test başarılı mıydı?
- Birden fazla tester aynı düzeltme hakkında farklı sonuç verdi mi?
- Hangi tür problemler daha sık görülüyor?
- Hangi Mission daha fazla sorun çıkarıyor?
- Hangi build ile açık hata sayısı azaldı?
- Hangi build regression oluşturdu?
- Bir hatanın ortalama çözülme süresi ne?
- Testerların geri bildirimleri ne kadar kullanılabilir?
- Proje bitince dosyalar nasıl arşivlenecek?
- Proje tamamen silinmek istenirse yanlışlıkla silinmesi nasıl önlenecek?

Bu soruların cevabını sonradan tahmin etmek yerine olay gerçekleştiği anda sistematik biçimde kaydetmek projenin ana felsefesidir.

---

# 2. Tasarım ilkeleri

## 2.1 Tester için aşırı basit kullanım

Testerların ortalama bilgisayar kullanıcısı olduğu varsayılır.

Testerın şunları bilmesi beklenmez:

- Hugging Face nedir?
- GitHub nedir?
- API nedir?
- token nasıl oluşturulur?
- bucket nasıl kullanılır?
- dosya hangi klasöre gönderilmeli?
- hata ID'si nasıl oluşturulmalı?
- JSON nedir?
- terminal nasıl kullanılır?

Testerın ideal akışı şudur:

1. Uygulamayı aç.
2. İlk kullanımda adını yaz.
3. Kendisine atanmış projeyi gör.
4. Ortak görev ve son tarihi gör.
5. En güncel test sürümünü gör.
6. Uygulama içinden sürümü indir.
7. Kurulum açıklamasını oku.
8. Testi yap.
9. Sorun görürse **Hata Raporla** butonuna bas.
10. Hata türünü seç.
11. Açıklamasını yaz.
12. İsterse video ekle.
13. Gönder.

Geride kalan teknik işlemler istemci ve backend tarafından yapılır.

## 2.2 Ölçemediğimiz şeye sahte yüzde vermemek

Bir Mission için:

> Mission %72 test edildi.

şeklinde bir metrik üretmek güvenilir değildir.

Bir tester bir bölümü sonuna kadar oynamış olsa bile daha önce denenmemiş bir checkpoint, hızlı dövüş, ara sahne atlama veya sıra dışı oyuncu davranışı yeni bir hata oluşturabilir.

Bu nedenle platformdaki çözüm çubuğu yalnızca **bilinen ve geçerli sorunların yaşam döngüsünü** gösterir.

Örnek:

```text
Bilinen geçerli sorun: 40
Çözüldü:             20
Üzerinde çalışılıyor: 8
Yeniden test:          5
Yeni:                  7
```

Sistem burada:

> **Bilinen sorunların %50'si çözüldü.**

ifadesini kullanır.

Sistem:

> Test %50 tamamlandı.

ifadesini kullanmaz.

## 2.3 Önemli değişiklikler üzerine yazılmaz

Bir hata bugün `Çözüldü` durumundaysa yalnızca bug tablosunda son durumun tutulması yeterli değildir.

Aşağıdaki geçmiş korunmalıdır:

```text
Yeni
↓
İncelendi
↓
Üzerinde Çalışılıyor
↓
Yeniden Test Bekliyor
↓
Yeniden Test Başarısız
↓
Üzerinde Çalışılıyor
↓
Yeniden Test Bekliyor
↓
Yeniden Test Başarılı
↓
Çözüldü
```

Bu sayede gelecekte:

- kaç bug ilk yeniden testte başarısız oldu,
- hangi hata türü en uzun sürüyor,
- hangi buildde regression arttı,
- bir bug kaç kere yeniden açıldı

gibi sorular gerçek geçmişten hesaplanabilir.

## 2.4 Hassas anahtarlar masaüstü uygulamasında tutulmaz

Hugging Face yazma yetkisine sahip ana token Tester veya Yönetim Merkezi EXE içine gömülmez.

İstemci yalnız QA backend ile konuşur.

Backend Hugging Face ile konuşur.

## 2.5 GitHub çalışma veritabanı değildir

GitHub:

- kaynak kod,
- CI,
- pull request,
- release pipeline,
- dokümantasyon

içindir.

Canlı QA durumu GitHub JSON commitleri olarak saklanmaz.

Örneğin:

- Ahmet yeniden test yaptı,
- bir hata çalışma kuyruğuna döndü,
- yeni build yayınlandı,
- kullanıcı cihaz değiştirdi,
- görev son tarihi değişti

gibi olayların ana kaynağı veritabanıdır.

---

# 3. Genel mimari

```text
                    ┌─────────────────────────────┐
                    │ Yönetim Merkezi             │
                    │ Admin / Developer Windows   │
                    └──────────────┬──────────────┘
                                   │
                                   │ HTTPS / API
                                   ▼
                     ┌─────────────────────────┐
                     │ Oyun QA Backend         │
                     │ FastAPI                 │
                     └───────────┬─────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              │                  │                  │
              ▼                  ▼                  ▼
         PostgreSQL        Hugging Face        Bildirim /
         QA metadata       Storage Bucket      Release verisi
              ▲                  ▲
              │                  │
              └────────┬─────────┘
                       │
                       │ HTTPS / API
                       ▼
                ┌───────────────┐
                │ Tester Client │
                │ Windows WPF   │
                └───────┬───────┘
                        │
                        ▼
                ┌───────────────┐
                │ Sabit Launcher│
                │ Otomatik Update│
                └───────────────┘
```

Ana görev ayrımı:

| Katman | Görev |
|---|---|
| PostgreSQL | Kullanıcılar, görevler, buglar, yeniden testler, durumlar, olay geçmişi |
| Hugging Face Storage Bucket | Büyük video/görsel/build dosyaları |
| GitHub | Kaynak kod, CI, PR, release otomasyonu |
| Tester Client | Tester için sade kullanıcı deneyimi |
| Yönetim Merkezi | Admin ve developer operasyonları |
| Launcher | İstemci güncelleme ve sürüm doğrulama |

---

# 4. Ana bileşenler

## 4.1 Tester Client

Testerların kullandığı Windows uygulamasıdır.

Şu anki temel çalışan akışta:

- kullanıcı adıyla ilk cihaz eşleştirmesi,
- güvenli oturum saklama,
- atanmış projeleri alma,
- aktif görevi gösterme,
- son tarihi gösterme,
- güncel test sürümünü gösterme,
- bilinen sorun çözüm oranını gösterme,
- bekleyen yeniden test sayısını gösterme,
- hata raporu oluşturma,
- video/görsel kanıt yükleme,
- testerın yeniden test sonucunu gönderme,
- yeniden teste video ekleme

bulunmaktadır.

İlerleyen aşamalarda bildirim, build indirme ve daha zengin ortak görev ekranları aynı istemciye bağlanacaktır.

## 4.2 Yönetim Merkezi

Admin ve developerların kullandığı ayrı Windows uygulamasıdır.

Temel çalışan katmanda:

- yönetici/geliştirici cihaz oturumu,
- rol doğrulaması,
- proje listesini alma,
- proje çözüm özetini gösterme,
- admin kullanıcı oluşturma,
- kullanıcıyı etkin/devre dışı bırakma,
- cihaz eşleşmesini sıfırlama

bulunmaktadır.

Yönetim Merkezi arayüzünde ayrıca aşağıdaki çalışma alanları hazırlanmıştır:

- Genel Bakış
- Hata Raporları
- Yeniden Testler
- Test Yönetimi
- Ekip Yönetimi
- Test Sürümleri
- Bildirimler
- İstemci Sürümleri
- Proje Ayarları

Bu çalışma alanlarının bir kısmı henüz gerçek API verisine tam bağlanma aşamasındadır.

## 4.3 Backend API

FastAPI tabanlı servis katmanıdır.

Sorumlulukları:

- kimlik doğrulama,
- rol kontrolü,
- proje kapsamı kontrolü,
- görev yönetimi,
- hata kayıtları,
- yeniden testler,
- build metadata,
- HF depolama işlemleri,
- audit eventler,
- analytics sorguları,
- ileride bildirim/WebSocket ve purge işlemleri.

## 4.4 Shared .NET katmanı

Tester ile Yönetim Merkezi aynı API sözleşmelerini kullanır.

Bu katmanda:

- ortak DTO'lar,
- API istemcisi,
- Türkçe metin/durum çeviri katmanı,
- tarih ve dosya boyutu biçimlendirmeleri

bulunur.

## 4.5 Launcher

Kullanıcıya ilk kez verilen kalıcı başlatıcıdır.

Hedef:

> Her uygulama güncellemesinde testerlara yeni EXE dağıtmak zorunda kalmamak.

Launcher yeni sürüm varsa indirir, SHA-256 doğrulaması yapar ve doğru payload'ı açar.

---

# 5. Kullanıcı rolleri

## 5.1 Test Ekibi Üyesi

Teknik enum: `tester`

Yetkiler:

- atanmış projeleri görme,
- görevleri görme,
- build bilgisini görme,
- hata raporlama,
- kendi raporuna video ekleme,
- kendisine atanmış yeniden testleri görme,
- yeniden test sonucu gönderme,
- yeniden test kanıtı ekleme.

## 5.2 Geliştirici

Teknik enum: `developer`

Tester yetkilerine ek olarak gelecekte/ilgili yönetim ekranlarında:

- build oluşturma,
- build dosyası yükleme,
- değişiklik notu yazma,
- kurulum talimatı yazma,
- fix candidate işaretleme

işlemlerini yapar.

Geliştirici kullanıcı/cihaz yönetimi gibi kritik admin yetkilerine sahip değildir.

## 5.3 Yönetici

Teknik enum: `admin`

Yetkileri:

- ekip üyesi oluşturma,
- kullanıcı etkin/devre dışı,
- cihaz sıfırlama,
- proje/görev yönetimi,
- build yayınlama,
- bug durumu yönetimi,
- yeniden test isteme,
- bildirim yönetimi,
- analytics,
- build arşivleme,
- proje kapatma,
- purge sürecine katılma.

## 5.4 Süper Yönetici

Teknik enum: `super_admin`

En yüksek riskli sistem işlemleri içindir.

Normal kullanıcı oluşturma ekranından yeni Süper Yönetici açılamaz.

---

# 6. Tester uygulaması

## 6.1 Tester önceden sistemde oluşturulur

Tester uygulamayı ilk kez açmadan önce admin **Ekip Yönetimi** ekranından tester adını oluşturur.

Örnek:

```text
Ad: Ahmet
Rol: Test Ekibi Üyesi
```

Testerın kendi hesabını uygulama içinden serbestçe oluşturması istenmez.

Bunun nedeni:

- kimlerin test ekibinde olduğunun kontrol edilmesi,
- rastgele kişilerin yalnız isim yazarak sisteme girmesinin engellenmesi,
- cihaz eşleştirmesinin yönetici kontrolünde olmasıdır.

## 6.2 İlk açılış

Tester yalnız şunu görür:

```text
DmC Türkçe Dublaj Test

Adınız
[ Ahmet                  ]

[ Devam ]
```

Arka planda uygulama:

1. yerel rastgele kurulum kimliği üretir,
2. backendde `Ahmet` kullanıcısını arar,
3. bu kullanıcı başka aktif cihaza bağlı mı kontrol eder,
4. uygunsa cihaz kaydı oluşturur,
5. rastgele cihaz credential'ı alır,
6. credential'ı Windows DPAPI ile şifreler,
7. sonraki açılışlarda kullanıcıya tekrar sormadan oturumu doğrular.

## 6.3 Ana ekran

Ana ekranda:

- kullanıcı adı,
- bağlantı durumu,
- projeler,
- aktif ortak görev,
- görevin son tarihi,
- güncel test sürümü,
- yeniden test sayısı,
- bilinen sorun çözüm oranı,
- hata raporlama butonu

bulunur.

## 6.4 İnternet bağlantısı kesilirse

Yerel güvenli credential hemen silinmez.

Uygulama:

> Bağlantı yok

şeklinde durum gösterir.

Ancak backend credential'ı gerçekten reddeder veya admin cihazı revoke ederse kullanıcı yeniden eşleştirme ekranına döner.

---

# 7. Yönetim Merkezi

## 7.1 Yönetici/geliştirici girişi

Yönetim Merkezi Tester Client'tan ayrı cihaz oturumu kullanır.

İlk kullanımda admin veya developer kendi adını girer.

Backend `client_kind=admin` bilgisini alır.

Normal `tester` rolü bu istemci üzerinden cihaz enrollment yapamaz.

## 7.2 Ekip Yönetimi

Yalnız yönetici rollerine görünür.

Admin şunları yapabilir:

- yeni Tester oluştur,
- yeni Geliştirici oluştur,
- yeni Yönetici oluştur,
- hesabı devre dışı bırak,
- hesabı yeniden etkinleştir,
- aktif cihaz sayısını gör,
- cihaz eşleşmesini sıfırla.

Örnek cihaz değişimi:

1. Ahmet eski bilgisayarı kullanmayı bırakır.
2. Admin Ahmet'i seçer.
3. **Cihaz Eşleşmesini Sıfırla** der.
4. Eski credential revoke edilir.
5. Ahmet yeni bilgisayarda uygulamayı açar.
6. Adını yazar.
7. Yeni cihaz güvenli biçimde eşleşir.

## 7.3 Proje özeti

Yönetim Merkezi seçilen proje için şu temel değerleri gerçek API'den alır:

- toplam rapor,
- aktif sorun,
- yeniden test bekleyen,
- çözülen,
- çözüm oranı.

Çözüm çubuğundaki segmentler veriyle dinamik büyür/küçülür.

---

# 8. Güvenli cihaz eşleştirmesi

## Neden donanım seri numarası kullanılmıyor?

Bir bilgisayarın anakart seri numarası, MAC adresi veya benzeri donanım verilerini zorunlu fingerprint haline getirmek:

- gereksiz veri toplama,
- donanım değişiminde kırılma,
- sanal makine/Windows güncellemesi sorunları

oluşturabilir.

Bu nedenle istemci ilk çalıştırmada rastgele `installation_id` üretir.

Backend bunun SHA-256 özetini cihaz fingerprint'i olarak saklar.

## Cihaz credential'ı

Backend rastgele uzun bir credential üretir.

Veritabanında credential'ın kendisi değil hash'i tutulur.

İstemci tarafında credential Windows `ProtectedData / CurrentUser` kullanılarak şifrelenir.

Böylece `client.json` dosyası düz metin bearer token içermez.

## HF anahtarıyla farkı

Tester cihaz credential'ı yalnız QA API'ye erişir.

Testerın elinde Hugging Face write token yoktur.

---

# 9. Proje yapısı ve çoklu proje desteği

En üst nesne `Project`tir.

Örnek:

```text
Oyun QA Platformu
│
├── DmC: Devil May Cry Türkçe Dublaj
├── Project B
├── Project C
└── Project D
```

Her proje kendi:

- üyelerine,
- sectionlarına,
- buildlerine,
- görevlerine,
- buglarına,
- yeniden testlerine,
- bildirimlerine,
- analytics verilerine,
- HF storage namespace'ine

sahiptir.

Bir tester yalnız kendisinin üyesi olduğu projeleri görür.

## Section etiketi

Projeye göre bölüm adı değişebilir:

- Mission
- Chapter
- Episode
- Level
- Bölüm

Bu nedenle veritabanı alanı generic `Section` olarak tasarlanmıştır.

---

# 10. Ortak görev sistemi

Bir görev üç testera verilmişse üç ayrı kopya oluşturmak yerine tek shared task kullanılır.

Örnek:

```text
Görev: Mission 04 Tam Bölüm Testi
Test Sürümü: 0.6.4
Son Tarih: 24 Ağustos 2026

Atananlar:
- Ahmet
- Mehmet
- Burak
```

Üç tester aynı görev kimliğini görür.

Ortak bilgiler:

- görev başlığı,
- açıklama,
- gerekli build,
- son tarih,
- görevdeki testerlar,
- görevle ilişkili raporlar.

Kişisel bilgiler:

- benim raporlarım,
- benden beklenen yeniden testler,
- benim bildirim okuma durumum.

---

# 11. Son tarih sistemi

Deadline ilerleme yüzdesi değildir.

Bir görev için:

```text
Mission 05 Tam Test
59 gün kaldı
```

gösterilebilir.

Zaman yaklaştıkça görsel önem artırılabilir:

- uzun süre → normal,
- 7 gün → sarı,
- 3 gün → turuncu,
- 24 saat → kırmızı,
- süre geçti → gecikmiş.

Son tarih ayrı bir zaman metriğidir.

Bilinen sorun çözüm çubuğuyla karıştırılmaz.

---

# 12. Test sürümü / build yönetimi

Build sistemin ana nesnelerinden biridir.

## Build durumları

Teknik durumlar:

```text
uploading
candidate
current
superseded
archived
```

Türkçe arayüzde:

```text
Yükleniyor
Aday Test Sürümü
Güncel Test Sürümü
Yerine Yeni Sürüm Geldi
Arşivlendi
```

gösterilir.

## Candidate ve Current farkı

Developer yeni dosya yüklediğinde bu sürüm anında herkese dağıtılmaz.

Önce `Candidate` olur.

Admin kontrol ettikten sonra:

> Güncel Test Sürümü Olarak Yayınla

kararı verir.

Bu ayrım yanlış/eksik buildin bütün tester ekibine gitmesini önler.

## Build metadata

Her build için:

- sürüm,
- başlık,
- açıklama,
- changelog,
- kurulum talimatı,
- dosya adı,
- dosya boyutu,
- SHA-256,
- yükleyen,
- yayınlayan,
- yüklenme tarihi,
- yayın tarihi,
- arşiv tarihi

saklanabilir.

## Build ile bug ilişkisi

Bir bug `reported_build_id` taşır.

Bu sayede:

> Bu hata hangi sürümde vardı?

sorusunun cevabı korunur.

## Fix candidate

Developer bir build oluştururken hangi bugların bu buildde düzeltilmiş olması beklendiğini belirleyebilir.

Bu buglar yeniden test akışına sokulabilir.

---

# 13. Build indirme ve kurulum açıklamaları

Tester uygulamasındaki hedef deneyim:

```text
GÜNCEL TEST SÜRÜMÜ
0.6.4
Mission 04 Ses Düzeltmeleri

[ Test Sürümünü İndir ]
[ Kurulum Talimatları ]
```

Tester Discord geçmişinden kurulum mesajı aramak zorunda kalmamalıdır.

Build açıklamasında örneğin:

```text
Kurulum:
1. Eski DmC_Dub klasörünü kaldırın.
2. ZIP içeriğini oyun klasörüne çıkartın.
3. Oyunu yeniden başlatın.
```

bulunabilir.

Build download/install masaüstü akışının backend temeli vardır; istemciye tam indirme ve kuruldu işaretleme bağlantısı sonraki geliştirme aşamalarındadır.

---

# 14. Hata raporlama

Tester için gerçek Türkçe hata raporu penceresi bulunmaktadır.

## Alanlar

- Proje
- Ortak görev
- Test sürümü
- Başlık
- Hata türü
- Oluşma koşulu
- Açıklama
- Kaç kez denendi
- Kaçında oluştu
- Videodaki hata anı
- Video/görsel kanıt

## DmC başlangıç hata türleri

- Eksik Türkçe Ses
- Yanlış Replik / Yanlış Ses
- Senkron Problemi
- Ses Seviyesi / Mix Problemi
- Ses Kalitesi Problemi
- Kesilen Replik
- Tekrarlanan Replik
- Teknik Problem
- Diğer

## Oluşma koşulları

- Normal oynanış
- Ara sahne atlandıktan sonra
- Ölüm / Checkpoint sonrası
- Mission yeniden başlatıldıktan sonra
- Dövüş çok hızlı bitirildiğinde
- Dövüş uzun sürdüğünde
- Boss / sahne geçişinde
- Bilinmiyor / Emin değilim

## Root cause neden testera sorulmuyor?

Tester:

> Checkpoint sonrası ses iki kez çaldı.

diyebilir.

Bunun gerçek teknik nedeni:

- state reset,
- yanlış trigger,
- asset mapping,
- entegrasyon,
- engine davranışı

olabilir.

Testerın teknik kök neden tahmini zorunlu değildir.

Root cause admin/developer değerlendirmesidir.

---

# 15. Video ve kanıt dosyaları

Video kanıt API'si çalışmaktadır.

Desteklenen istemci dosya türleri arasında:

- MP4
- MOV
- MKV
- WebM
- AVI
- PNG
- JPG/JPEG

bulunur.

## Bug videosu

Dosya yolu örneği:

```text
projects/
  <project-id>/
    reports/
      <bug-key>/
        evidence/
          <asset-id>-video.mp4
```

## Yeniden test videosu

```text
projects/
  <project-id>/
    retests/
      <retest-request-id>/
        evidence/
          <asset-id>-video.mp4
```

## Kaydedilen metadata

- evidence ID,
- proje,
- sahip nesne,
- orijinal dosya adı,
- medya türü,
- dosya boyutu,
- SHA-256,
- yükleyen kullanıcı,
- tarih,
- storage path.

## Varsayılan üst sınır

Backend varsayılan olarak video/kanıt dosyası için 20 GB üst limit kullanır.

Bu değer environment ayarıyla değiştirilebilir.

## Duplicate-safe video tekrar denemesi

Önemli kullanıcı deneyimi davranışı:

1. Tester **Raporu Gönder** der.
2. Bug kaydı başarıyla oluşur.
3. İnternet video yüklenirken kopar.
4. Ekranda rapor ID'si korunur.
5. Kullanıcı **Videoyu Tekrar Yükle** der.
6. İkinci bug oluşturulmaz.
7. Yalnız evidence tekrar yüklenir.

Bu davranış ağ hatalarının duplicate bug üretmesini önler.

---

# 16. Hata yaşam döngüsü

Ana akış:

```text
Yeni
↓
İncelendi
↓
Üzerinde Çalışılıyor
↓
Yeniden Test Bekliyor
↓
Çözüldü
```

Yan durumlar:

- Beklemede
- Tekrar Rapor
- Hata Değil
- Düzeltilmeyecek
- Yeniden Açıldı

## BugEvent

Durum değişiklikleri event olarak kaydedilir.

Örnek:

```text
10 Ağustos 14:31 — Ahmet rapor oluşturdu
10 Ağustos 15:06 — Hasan raporu inceledi
11 Ağustos 09:22 — Üzerinde çalışılıyor
13 Ağustos 18:14 — 0.6.2 için yeniden test istendi
14 Ağustos 11:42 — Yeniden test başarısız
15 Ağustos 10:17 — Tekrar çalışmaya alındı
17 Ağustos 16:21 — 0.6.3 için yeniden test istendi
18 Ağustos 13:04 — Yeniden test başarılı
18 Ağustos 13:32 — Yönetici tarafından çözüldü
```

---

# 17. Yeniden test sistemi

Tester tarafında gerçek yeniden test ekranı bulunmaktadır.

## Admin talebinin veri yapısı

Bir yeniden test talebinde:

- bug,
- gerekli build,
- isteyen admin,
- atanan testerlar,
- açıklama,
- son tarih

saklanır.

## Aynı talep birden fazla testera atanabilir

Örnek:

```text
DMC-0042
Build 0.6.5

Atananlar:
- Ahmet
- Mehmet
- Burak
```

Bu tek bir retest request'tir.

Her tester kendi sonucunu ayrı verir.

Ahmet sonuç verdikten sonra:

- Ahmet'in bekleyen listesinden düşer,
- Mehmet'in listesinden düşmez,
- Burak'ın listesinden düşmez.

## Testerın gördüğü sonuç seçenekleri

Teknik enum yerine:

- **Sorun artık oluşmuyor**
- **Sorun hâlâ oluşuyor**
- **Emin olamadım**

seçenekleri gösterilir.

## Yeniden test videosu

Tester yeniden test sonucuna video/görsel kanıt ekleyebilir.

Kanıt sonuç gönderilmeden önce yüklenir.

## Başarısız sonuç

Tek bir başarısız sonuç sorunu tekrar çalışma kuyruğuna döndürmek için yeterli kabul edilir.

Örneğin:

```text
Ahmet   Başarılı
Mehmet  Başarılı
Burak   Başarısız
```

sistem bugı otomatik çözülmüş saymaz.

## Başarılı sonuç

Başarılı tester sonucu bugı doğrudan `Çözüldü` yapmaz.

Final kapatma admin kararıdır.

---

# 18. Bildirim ve duyurular

Bildirim veri modeli hazırdır; uçtan uca canlı bildirim sistemi geliştirme sırasındadır.

Hedef bildirim türleri:

- bilgi,
- uyarı,
- kritik,
- okunması zorunlu.

Hedef gruplar:

- herkes,
- proje üyeleri,
- belirli ortak görev üyeleri,
- seçili testerlar,
- tek kullanıcı.

## Okunması zorunlu duyuru

Örnek:

> Mission 06 buildinde bozuk dosya tespit edildi. Yeni build yayınlanana kadar testi durdurun.

Admin daha sonra:

```text
Ahmet   Okudu
Mehmet  Okudu
Burak   Okumadı
```

gibi receipt bilgisi görebilmelidir.

Planlanan transport:

- uygulama açıkken WebSocket,
- bağlantı geri geldiğinde normal API senkronizasyonu,
- ileride system tray / Windows bildirimi.

---

# 19. İstatistik ve analiz yaklaşımı

Analytics platformun ana özelliklerinden biri olacaktır.

## Ana kural

Grafik yalnız dekorasyon değildir.

Örneğin:

> 19 Yeniden Test Bekliyor

kartına tıklandığında mümkün olduğunca bu 19 raporun filtrelenmiş listesine gidilmelidir.

## Ana metrikler

- Toplam rapor
- Geçerli bilinen sorun
- Çözülen
- Üzerinde çalışılan
- Yeniden test bekleyen
- Yeni / henüz ele alınmayan
- Beklemede
- Duplicate
- Hata değil
- Yeniden açılan
- Regression oranı
- Çözüm oranı

## Bug türü analizi

Örnek:

```text
Eksik Türkçe Ses       52
Senkron                 47
Mix                     38
Kesilen Replik          31
Yanlış Replik           22
```

Her tür için:

- çözüm oranı,
- ortalama çözüm süresi,
- yeniden test başarı oranı,
- reopen oranı

hesaplanabilir.

## Mission / Section analizi

```text
Mission 01   32 sorun   30 çözüldü
Mission 02   41 sorun   35 çözüldü
Mission 03   67 sorun   31 çözüldü
```

## Zaman metrikleri

Event geçmişi sayesinde:

- rapor → ilk admin incelemesi,
- inceleme → çalışmaya başlama,
- çalışmaya başlama → yeniden test,
- ilk rapor → çözüm

süreleri hesaplanabilir.

---

# 20. Tester istatistikleri

Platform "kim en çok bug buldu" yarışına dönüştürülmeyecektir.

En çok bug bulan kişi leaderboardu:

- gereksiz rapor,
- duplicate rapor,
- kalite yerine sayı odaklı davranış

teşvik edebilir.

Bunun yerine sağlıklı kalite metrikleri tutulur:

- toplam gönderim,
- doğrulanmış sorun,
- duplicate,
- hata değil,
- video ekleme oranı,
- reproduction bilgisi ekleme oranı,
- tamamlanan yeniden test,
- yeniden test cevap oranı,
- ağırlıklı olarak bulduğu hata türleri.

Bu bir skor tablosu değil, tester geri bildirim profilidir.

---

# 21. Build istatistikleri

Buildler karşılaştırılabilir olmalıdır.

Örnek:

```text
Build      Açık   Yeni   Çözülen   Regression
0.6.2       78     31       18          8
0.6.3       64     21       35          3
0.6.4       43     12       33          2
0.6.5       19      5       29          0
```

Her build için ileride:

- kaç tester indirdi,
- kaç kişi kuruldu olarak işaretledi,
- kaç hata raporlandı,
- kaç fix candidate vardı,
- kaç yeniden test geçti,
- kaç yeniden test başarısız oldu,
- kaç regression açıldı

gösterilebilir.

---

# 22. Hugging Face depolama düzeni

Büyük dosyalar için **Hugging Face Storage Bucket** kullanılmaktadır.

Ana bucket:

```text
xykeskin/dmc-turkish-dub-qa-archive
```

Bu sistem kullanıcının mevcut diğer Hugging Face datasetlerini kullanmaz veya değiştirmez.

## Proje bazlı namespace

```text
projects/
  <project-id>/
    reports/
    retests/
    builds/
      active/
      archived/
```

## Örnek

```text
projects/
  project_dmc_001/
    reports/
      DMC-000001/
        evidence/
          asset123-video.mp4

    retests/
      retest-001/
        evidence/
          asset456-video.mp4

    builds/
      active/
        build-00042/
          DmC-Dub-0.6.4.zip

      archived/
        build-00041/
          DmC-Dub-0.6.3.zip
```

Storage path uygulama tarafından otomatik üretilir.

Tester klasör oluşturmaz.

## Neden metadata HF JSON'u değil?

Hugging Face büyük binary depolama katmanıdır.

QA state'in ana kaynağı ilişkisel veritabanıdır.

---

# 23. Uygulama güncelleme sistemi

Testerın her yeni sürümde farklı EXE indirmesi hedeflenmez.

Kullanıcıya sabit bir launcher verilir.

Örnek:

```text
DmC-Dub-QA.exe
```

Launcher:

1. QA backendden güncel istemci manifestini ister.
2. Yerel sürümle karşılaştırır.
3. Yeni sürüm varsa paketi indirir.
4. SHA-256 doğrulaması yapar.
5. Yeni sürümü ayrı klasöre çıkarır.
6. Doğru istemci EXE'sini çalıştırır.
7. Güncelleme başarısız olursa mevcut çalışan sürümden devam etmeyi mümkün kılar.

## Kanallar

- Kararlı
- Beta

Normal testerlar Kararlı kanalını kullanabilir.

Admin/geliştirici ekibi Beta kanalında yeni özellikleri deneyebilir.

## Güncelleme notları

Her release için:

- sürüm,
- başlık,
- notlar,
- kanal,
- zorunlu olup olmadığı

saklanabilir.

---

# 24. Türkçe dil desteği

Platformun son kullanıcı ana dili **Türkçe**dir.

Bu yalnızca birkaç butonun çevrilmesi anlamına gelmez.

## Türkçe olması gerekenler

- pencere başlıkları,
- menüler,
- butonlar,
- tablo başlıkları,
- durum adları,
- rol adları,
- hata mesajları,
- bağlantı mesajları,
- bildirimler,
- son tarih metinleri,
- tarih formatları,
- dosya boyutları,
- güncelleme uyarıları,
- tester hata formu,
- yeniden test formu,
- yönetim giriş ekranı,
- ekip yönetimi.

## Merkezi çeviri katmanı

`src/Shared/TurkishUi.cs`

teknik enumları kullanıcıya Türkçe gösterir.

Örnek:

```text
in_progress       → Üzerinde Çalışılıyor
retest_required   → Yeniden Test Bekliyor
resolved          → Çözüldü
duplicate         → Tekrar Rapor
not_a_bug         → Hata Değil
wont_fix          → Düzeltilmeyecek
reopened          → Yeniden Açıldı
candidate         → Aday Test Sürümü
current           → Güncel Test Sürümü
```

## Kültür

Tester, Admin ve Launcher `tr-TR` kültürünü kullanır.

Böylece tarihler örneğin:

```text
10 Ağustos 2026 14:32
```

formatında gösterilebilir.

---

# 25. Güvenlik modeli

## 25.1 HF write token EXE içinde değildir

Bu temel kuraldır.

Tester veya admin EXE reverse-engineer edilse bile ana HF write tokenın istemcide bulunmaması hedeflenir.

## 25.2 Device credential ayrı yetkidir

Device credential yalnız QA API oturumu içindir.

## 25.3 Server tarafında credential hash tutulur

Düz credential veritabanında saklanmaz.

## 25.4 İstemcide DPAPI

Windows kullanıcı profiline bağlı ProtectedData kullanılır.

## 25.5 Rol kontrolü yalnız UI gizleme değildir

Örneğin Ekip Yönetimi butonunu developerdan gizlemek tek güvenlik değildir.

Backend `/admin/users` endpointi de admin rolü ister.

## 25.6 Proje kapsamı

Normal kullanıcı başka proje ID'sini tahmin ederek o projenin kanıt dosyasını indirmemelidir.

Evidence endpointlerinde proje üyeliği kontrolü bulunur.

## 25.7 Başka testerın raporuna dosya ekleme

Normal tester yalnız kendi hata raporuna evidence yükleyebilir.

Admin/developer için daha geniş inceleme yetkileri tanımlanabilir.

## 25.8 Yeniden test evidence yetkisi

Normal tester yalnız kendisine atanmış yeniden test talebine evidence ekleyebilir.

---

# 26. Veritabanı ve olay geçmişi

Temel domain tabloları:

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
NotificationReceipts
ClientReleases
AuditEvents
PurgeRequests
```

## BugEvent

Bug yaşam döngüsünü tarihsel olarak saklar.

## AuditEvent

Bug dışı önemli işlemleri saklar.

Örnek:

- cihaz eşleşti,
- kullanıcı oluşturuldu,
- cihazlar sıfırlandı,
- build dosyası yüklendi,
- build indirilmeye başladı,
- build arşivlendi,
- evidence yüklendi.

## Production veritabanı

Hedef PostgreSQL'dir.

Local geliştirmede SQLite kullanılabilir.

---

# 27. Proje kapatma, arşivleme ve kalıcı silme

İki kavram birbirinden ayrıdır.

## 27.1 Projeyi Kapat

```text
ACTIVE
↓
CLOSED
```

Bu işlem veri silmez.

Amaç:

- yeni normal test akışını durdurmak,
- projeyi aktif tester ekranından çıkarmak,
- geçmiş verileri admin incelemesine açık tutmak.

## 27.2 Kalıcı Silme / Purge

Bu geri alınamaz işlem için yüksek güvenlik hedeflenmektedir.

Planlanan zorunlu adımlar:

1. Proje önce Kapalı olmalı.
2. Silinecek kapsam önizlenmeli.
3. Proje adı elle yazılmalı.
4. Rastgele doğrulama kodu girilmeli.
5. Talebi bir admin başlatmalı.
6. İkinci farklı admin onaylamalı.
7. Yalnız ilgili project namespace silinmeli.
8. Silme sonucu audit edilmelidir.

Örnek önizleme:

```text
142 bug raporu
197 video
38 yeniden test videosu
24 görev
187 bildirim kaydı
128,7 GB Hugging Face verisi
```

## Build arşivleme purge değildir

Bir build arşivlendiğinde dosya proje içinde `active` alanından `archived` alanına taşınır.

Proje verisi silinmez.

---

# 28. Kaynak kod yapısı

```text
src/
├── api/
│   └── app/
│       ├── application.py
│       ├── main.py
│       ├── models.py
│       ├── schemas.py
│       ├── services.py
│       ├── db.py
│       ├── storage.py
│       ├── auth.py
│       ├── auth_api.py
│       ├── build_api.py
│       ├── evidence_api.py
│       ├── retest_api.py
│       └── release_api.py
│
├── Shared/
│   ├── Contracts.cs
│   ├── ApiClient.cs
│   └── TurkishUi.cs
│
├── TesterApp/
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── BugReportWindow.xaml
│   ├── BugReportWindow.xaml.cs
│   ├── RetestWindow.xaml
│   └── RetestWindow.xaml.cs
│
├── AdminApp/
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── AdminLoginWindow.xaml
│   ├── AdminLoginWindow.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── TeamManagementWindow.xaml
│   └── TeamManagementWindow.xaml.cs
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

# 29. Yerel geliştirme

> Normal testerların aşağıdaki adımları yapması gerekmez.

## Backend

Gereksinim:

- Python 3.13

Kurulum:

```bash
cd src/api
pip install -e '.[dev]'
```

Önemli environment değerleri:

```text
DATABASE_URL
HF_BUCKET_ID
HF_TOKEN
BOOTSTRAP_KEY
DEVICE_CREDENTIAL_SECRET
MAX_EVIDENCE_UPLOAD_BYTES
```

Gerçek isimler `Settings` modelindeki alanlarla eşleştirilir.

Ana güvenlik kuralı:

```text
HF_TOKEN kaynak koda commit edilmez.
```

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

# 30. CI ve derleme doğrulaması

GitHub Actions iki ana job çalıştırır.

## Backend job

- bağımlılık kurulumu,
- Python compile,
- FastAPI application import,
- correctness odaklı Ruff kontrolleri.

## Windows job

Gerçek Windows runner üzerinde:

- Shared,
- TesterApp,
- AdminApp,
- Launcher

derlenir.

Amaç yalnız kodun repoda görünmesi değil, platformun gerçek Windows derleyicisinden geçmesidir.

---

# 31. Mevcut geliştirme durumu

Bu tablo README'nin bu sürümündeki gerçek branch durumunu yansıtır.

| Bileşen | Durum |
|---|---|
| Çoklu proje domain modeli | Temel hazır |
| Kullanıcı / rol modeli | Temel hazır |
| Tam Türkçe masaüstü kültür ve ana UI metinleri | Temel hazır |
| Merkezi Türkçe durum/rol çeviri katmanı | Hazır |
| Tester cihaz eşleştirme | Uçtan uca temel hazır |
| Tester DPAPI güvenli oturum | Hazır |
| Yönetici/Geliştirici cihaz girişi | Uçtan uca temel hazır |
| Admin tester/developer/admin kullanıcı ön-kayıt | Hazır |
| Admin kullanıcı etkin/devre dışı | Hazır |
| Admin cihaz eşleşmesi sıfırlama | Hazır |
| Tester proje listesini gerçek API'den alma | Hazır |
| Tester ortak görev / son tarih özeti | Temel hazır |
| Tester güncel build bilgisi | Temel hazır |
| Tester bilinen sorun çözüm oranı | Temel hazır |
| Hata yaşam döngüsü | Temel hazır |
| Bug event geçmişi | Temel hazır |
| Türkçe hata raporu penceresi | Hazır |
| Video zaman kodu girişi | Hazır |
| Bug video/görsel evidence upload | Uçtan uca temel hazır |
| Evidence SHA-256 ve boyut kaydı | Hazır |
| Video upload duplicate-safe tekrar deneme | Hazır |
| Yeniden test veri modeli | Hazır |
| Tester bekleyen yeniden test listesi | Hazır |
| Tester yeniden test sonucu | Hazır |
| Tester yeniden test video upload | Hazır |
| Başarısız yeniden testin çalışma kuyruğuna dönüşü | Hazır |
| Başarılı yeniden testin admin onayı olmadan kapanmaması | Hazır |
| Yönetim Merkezi gerçek proje listesi | Hazır |
| Yönetim Merkezi temel proje analytics özeti | Hazır |
| Yönetim Merkezi dinamik çözüm çubuğu | Hazır |
| Build metadata modeli | Temel hazır |
| HF Storage Bucket adapter | Temel hazır |
| Build upload/download/archive backend API | Temel hazır |
| Tester build indirme masaüstü akışı | Geliştirilecek |
| Developer Build Center masaüstü upload akışı | Geliştirilecek |
| Admin bug detay + video oynatıcı | Geliştirilecek |
| Admin yeniden test oluşturma UI | Geliştirilecek |
| Ortak görev ayrıntı ekranı | Geliştirilecek |
| Çoklu proje arasında zengin tester gezinmesi | Geliştirilecek |
| Notification veri modeli | Temel hazır |
| WebSocket anlık bildirim | Geliştirilecek |
| MUST READ acknowledge akışı | Geliştirilecek |
| Derin analytics drill-down | Geliştirilecek |
| Tester analytics | Geliştirilecek |
| Build analytics | Geliştirilecek |
| Release veri modeli | Temel hazır |
| Sabit launcher + SHA-256 update temeli | Hazır |
| Client release yayın pipeline | Geliştirilecek |
| PostgreSQL migration / Alembic | Geliştirilecek |
| Purge veri modeli | Temel hazır |
| İki admin onaylı purge executor | Geliştirilecek |
| Production Hugging Face Space deployment | Geliştirilecek |

---

# 32. Yakın dönem geliştirme planı

Tamamlanan cihaz/video/tester-retest temellerinden sonra ana sıra:

1. Yönetim Merkezi hata listesi ve bug detay ekranını gerçek API'ye bağlamak.
2. Admin video oynatıcı ve rapordaki zaman koduna atlama.
3. Admin bug durum/root cause değişiklik ekranı.
4. Admin yeniden test oluşturma ekranı ve çoklu tester seçimi.
5. Ortak görev oluşturma/düzenleme UI'sı.
6. Görev panelinde ortak tester görünümü ve aktivite akışı.
7. Developer Build Center gerçek dosya yükleme akışı.
8. Admin Candidate → Güncel Test Sürümü yayın akışı.
9. Tester test sürümü indirme ve kurulum talimatı ekranı.
10. Build download/installed audit eventlerini istemciye bağlamak.
11. Bildirim API'si ve WebSocket fan-out.
12. Okunması zorunlu duyuru receipt akışı.
13. Windows system tray bildirimleri.
14. Analytics drill-down endpointleri.
15. Bug türü / Mission / build / retest grafik ekranları.
16. Tester kalite istatistikleri.
17. Build karşılaştırma ve regression analizi.
18. İstemci release yayınlama pipeline'ı.
19. PostgreSQL + Alembic migration.
20. İki farklı admin onaylı kalıcı proje silme yürütücüsü.
21. Production Hugging Face Space / servis deployment.

---

# 33. Terimler sözlüğü

## Project / Proje

Sistem üzerinde bağımsız yönetilen oyun, mod veya QA çalışması.

## Section

Mission, Chapter, Episode, Level gibi proje içi bölüm.

## Build / Test Sürümü

Testerların belirli bir test görevinde kullandığı oyun/mod/dublaj paketi sürümü.

## Candidate / Aday Test Sürümü

Developer tarafından yüklenmiş ancak henüz admin tarafından genel tester kullanımına açılmamış sürüm.

## Current / Güncel Test Sürümü

Admin tarafından o proje için testerların kullanması amacıyla aktif yayınlanan sürüm.

## Shared Task / Ortak Görev

Aynı test görevinin birden fazla tester tarafından tek ortak kayıt üzerinden yürütülmesi.

## Bug Report / Hata Raporu

Tester tarafından gönderilen sorun kaydı.

## Evidence / Kanıt

Bug veya yeniden test sonucunu destekleyen video/görsel dosya.

## Reproduction / Tekrar Edilebilirlik

Sorunun kaç denemenin kaçında görüldüğü.

## Retest / Yeniden Test

Düzeltildiği düşünülen bir sorunun belirli build üzerinde tekrar kontrol edilmesi.

## Fix Candidate

Developerın belirli buildde düzeltilmiş olması gerektiğini bildirdiği bug.

## Reopened / Yeniden Açıldı

Daha önce kapatılan sorunun yeniden ortaya çıkması.

## Regression

Yeni değişiklik nedeniyle eski davranışın bozulması veya çözülmüş problemin tekrar ortaya çıkması.

## Audit Event

Sistemde gerçekleşmiş önemli bir işlemin tarihsel kaydı.

## Device Enrollment / Cihaz Eşleştirmesi

Önceden oluşturulmuş kullanıcı hesabının belirli bir istemci kurulumuyla güvenli olarak eşleştirilmesi.

## Purge / Kalıcı Silme

Bir projenin dosya ve çalışma verilerinin geri alınamayacak biçimde, güvenlik kontrollerinden sonra silinmesi.

---

# Son not

Bu platformun başarısı yalnız çok fazla özellik içermesine bağlı değildir.

Asıl hedef:

- tester için son derece kolay,
- admin için son derece görünür,
- developer için pratik,
- veri açısından izlenebilir,
- güvenlik açısından kontrollü,
- çoklu proje açısından ölçeklenebilir,
- proje tamamlandığında yönetilebilir

bir QA süreci oluşturmaktır.

DmC: Devil May Cry Türkçe Dublaj projesi sistemin ilk gerçek kullanım alanıdır. Ancak altyapının uzun vadeli amacı aynı uygulamalar üzerinden farklı oyun ve mod projelerinin test süreçlerini ortak bir kalite yönetim standardıyla yürütebilmektir.
