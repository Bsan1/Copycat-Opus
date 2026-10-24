# Görsellerden gelişen 3D stil rehberi — uygulama planı

24 Ekim 2026. Durum: ilk uygulama ve içerik kaybını gideren ikinci revizyon doğrulandı. Kaynaklı stil verisi, iki belge, eksiksiz ilgili üretim brief'i, hedefli güncelleme ve teknik entegrasyon mevcut. Görsel stil içeriği kullanıcı onayı verilmiş sayılmayan taslaktır. Aşağıdaki ilk teslimat kaydı tarihseldir; güncel davranış ve doğrulama bu belgenin sonundadır.

## Hedef ve kapsam

Kullanıcı istediği görünümü taşıyan görselleri verir. AI, bunlardan ayrıntılı ve uygulanabilir bir 3D asset stil tanımı çıkarır. Aynı tanımdan iki çıktı üretilir: AI'ın kullanacağı kapsamlı rehber ve kullanıcının hedef görünümü anlayabileceği resimli rapor. Model üretimi ve kullanıcı geri bildirimi ilerledikçe stil kontrollü biçimde gelişir.

Kullanıcıdan her resmi etiketlemesi veya uzun formlar doldurması beklenmez. AI ilk yorumunu yapar; yalnızca sonucu belirgin biçimde değiştiren belirsizlikleri kısa seçeneklerle gösterir. Kullanıcının açık tercihleri yorumların önüne geçer.

Çalışma yalnızca **Copycat Opus oyun reposunda** yapılır. Kardeş `opus-model-pipeline` reposu kapsam dışıdır. İlk teslimat stil verisi, belgeler ve bunların üretim sürecidir. Model generator'ları, Unity materyalleri, ışık, post-processing ve oyun mekaniği bu teslimatın parçası olarak değiştirilmez.

## Başlangıç kaydı

- Oyun klasöründe Git geçmişi yoktu. Mevcut kaynaklar ve oyun asset'leri özel `Bsan1/Copycat-Opus` reposuna alındı.
- Başlangıç commit'i: `b62d5f0` — `Snapshot Copycat Opus game and existing model pipeline`.
- Çalışma branch'i: `codex/style-guideline`.
- `Library`, `Builds`, `Logs`, yerel ayarlar ve önbellekler kaynak kontrolü dışında. Başlangıçta yalnızca Python önbelleği ve geçici agent kilidi için ignore kuralları eklendi.
- İncelenen mevcut kayıtlar: 52 araç testi, 275 Unity testi ve beş Blender kontrolü başarılı. Bu plan hazırlanırken testler veya oyun yeniden çalıştırılmadı.

## Oyunda ve eski sistemde gördüklerimiz

Oyun; biçme, tırpan, sulama, bahçe alanları, görevler ve ilerleme sistemleri olan bir Unity projesi. Sekiz üretim modeli ve bir teknik kalibrasyon modeli mevcut. Kodla Blender üretimi, geometri kontrolleri, FBX/spec çıktısı, Unity import doğrulaması ve hash'e bağlı inceleme kayıtları kullanılabilir bir temel oluşturuyor.

Oyun görüntülerindeki yumuşaklık ve lavanta rengi sisin bir bölümü `Atmosphere.cs` ve `PostFx.cs` üzerinden geliyor. Görüntünün tamamındaki renk dağılımı bir objenin taban renk paleti olarak kullanılamaz. Rehber, görünen sahne etkisi ile objenin şekil ve yüzey özelliklerini ayırmalı.

Üç materyal yolu var: prosedürel yüzey ve vertex AO; renk/normal/ORM atlası; bina yüzeylerinde tekrar eden dokular. Stil kuralları önce görünümü tarif etmeli, sonra ilgili üretim yoluna uygulama notu vermeli.

Mevcut look-dev karşılaştırmasında ham FBX'ler kullanılıyor: biçme makinesinin tekerlekleri eklenmiyor, tırpan parçaları oyun içindeki konumlarına taşınmıyor. Atlas önizlemesi normal/ORM verisini kullanmadan roughness değerini sabitliyor. Bu görseller montaj, siluet veya malzeme doğruluğuna kanıt olarak kullanılmamalı. İlk raporda mevcut doğru monte edilmiş oyun görüntüleri ve uygun yakın çekimler kullanılacak; yeni nötr render ancak bir kararı vermek için gerekirse üretilecek.

## Tutulacak, düzeltilecek ve ayrılacak parçalar

| Parça | Karar |
| --- | --- |
| Blender generator'ları, `opus_model.py`, `qc.py`, Unity import/test hattı | Mevcut teknik altyapı olarak korunacak. |
| `Style.py` brief, rapor, görsel hazırlama ve şema denetimleri | Yeni stil tanımına uyarlanacak. |
| `style.json` kural kimlikleri, kapsam/sınıf ve sürüm bilgisi | Kaynaklar, uygulama açıklamaları ve karar kayıtlarıyla genişletilecek. |
| `Review.py` hash'e bağlı inceleme/onay ve konumlu bulgular | Korunacak; stil incelemesi aynı model incelemesine eklenecek. |
| Mevcut modellerden otomatik hedef paleti çıkarma | Hedef stili değiştiren davranış kaldırılacak. Mevcut modeller yalnızca açıkça seçilmiş uygulama örnekleri olacak. |
| `Style.py shots`, Verify'daki trailer karşılaştırması ve art bible'ın ilgili bölümü | Stil akışından çıkarılacak. Normal oyun senaryoları ve `Compare.py` geometrik karşılaştırması korunacak. |
| Açık/koyu piksellerden kesin ışık-gölge rengi çıkarımı; “her sayı ölçüldü” iddiası | Kaldırılacak. Ölçümler girdileri ve sınırlılıklarıyla açıklanacak. |
| “Teknik kontroller geçti = stil uygun” anlatımı | Teknik sonuç ile görsel inceleme durumu ayrı gösterilecek. |
| Her stil sürümü farkında modeli yeniden üretme beklentisi | Kural değişikliğinin kapsamına göre yeniden değerlendirme yapılacak; salt belge düzenlemesi build tetiklemeyecek. |

## Uygulama sırası

### 1. Kaynakları ve kararları açık hâle getir

`tools/blender/style/references.json` içinde her referansın kimliği, dosya/hash bilgisi, ilgili bölgesi ve kullanım amacı tutulacak. Bir görselin yalnızca renkleri isteniyorsa şekil kurallarına kaynak sayılmayacak. Birden fazla oyundan gelen tercihler sessizce ortalanmayacak.

`style.json` içindeki her kural şu bilgileri taşıyacak: kimlik, anlaşılır talimat, ayrıntılı uygulama açıklaması, asset/malzeme kapsamı, görsel kanıt veya kullanıcı kararı, belirsizlik, durum ve kontrol yöntemi. Gözlem, AI yorumu, kullanıcı tercihi ve teknik gereklilik ayrılacak. Görsellerden pah milimetresi veya kesin roughness ölçülmüş gibi sunulmayacak.

İlk görsel sentez bir kez yapılacak; sonraki işlemler kaydedilmiş kurallardan devam edecek. Mevcut 17 kırpıntı, bu oyuna ait başlangıç malzemesi olarak kullanılabilir; bunlardan türetilen stil kullanıcı tarafından henüz kabul edilmiş sayılmayacak.

### 2. Tek kaynaktan iki belge ve kısa üretim brief'i çıkar

- **AI rehberi:** `Builds/Style/style_guide_ai.md`. Şekil dili, oranlar, siluet, kenarlar, detay hiyerarşisi, malzeme/doku davranışı, renk kullanımı, sınıfa özgü uygulamalar, istisnalar, kaynaklar ve karar geçmişi ayrıntılı biçimde yer alacak.
- **Kullanıcı raporu:** mevcut `Builds/Style/art_bible.html` üreticisi yenilenecek. Başta hedef görünümün kısa tarifi ve görselleri; devamında her önemli tercihin örneği, etkisi ve gerekçesi gösterilecek. Ölçüm ayrıntıları isteğe bağlı açılacak. Hedef referans, mevcut üretim ve onaylı örnek açıkça etiketlenecek.
- **Model brief'i:** mevcut `Style.py brief` yoluyla yalnızca ortak kurallar, ilgili sınıf/malzemeler, seçilmiş örnekler ve gerekli istisnalar aktarılacak. Tam rehber her modelde bağlama yüklenmeyecek.

İki belge ayrı ayrı elle düzenlenmeyecek. Aynı kural verilerinden üretilecek; kural kimlikleri ve sürümler eşleşecek. Kavram seçim panoları gerekirse görsel üzerindeki etiketler yalnızca numara olacak; açıklamalar rapor metninde bulunacak.

### 3. Mevcut üretimle küçük bir prova yap

Yeni model üretmeden, mevcut biçme makinesi, ev/kulübe ve ağaç çıktıları üzerinden makine, bina ve organik sınıfları denenecek. Bunlar otomatik olarak doğru veya onaylı örnek sayılmayacak.

Prova şu soruyu yanıtlamalı: Bu brief, aynı sınıfta farklı bir nesne yapacak AI'a yeterli yön veriyor mu? Yalnızca trailer'daki tek objeyi kopyalamayı tarif ediyorsa kurallar genelleştirilecek. Yetersiz görsel kanıt varsa yalnızca ilgili yakın çekim/render üretilecek. Sırf süreç tamamlamak için her stilde iki yeni pilot zorunlu olmayacak.

### 4. Model geri bildirimini sınırlı ve izlenebilir güncelle

Model onayı, o modelin her özelliğini bütün oyun için yeni standart yapmayacak. Kullanıcının örneğin “ahşabı bundan sonra böyle yapalım” demesi ilgili malzeme kuralını ve örneğini güncelleyecek. Bir modele özel düzeltme o modelde kalacak.

Örnek olarak seçilen asset tam onaylı sürümüne ve görseline bağlanacak. `Style.py` şu an approvals dosyasındaki model adını kullanıyor; onayın mevcut asset hash'ine uyup uymadığını doğrulamıyor. Bu açık düzeltilecek.

Belge revizyonu ve üretimi etkileyen kural revizyonu ayrılacak. Değişen kural için ilgili sınıflar/asset'ler listelenecek; önce yeniden değerlendirme yapılacak, ancak gerekliyse model değiştirilecek. Küçük geri bildirimde kullanıcıya değişen bölüm gösterilecek.

### 5. Kontrolleri ve çalışma talimatlarını yeni akışa bağla

`Verify.ps1` teknik stil kurallarını ve görsel inceleme durumunu doğru isimlerle raporlayacak. Görsel benzerlik için kanıtlanmamış bir puan bloklayıcı yapılmayacak. Teknik bütçeler kaynaklarıyla belirtilecek; bütçe kontrolü sanat tercihi gibi sunulmayacak.

Oyundaki `.agents/skills/art-style` ve `.claude/skills/art-style` talimatları yeni akışla uyumlu olacak. Model-plausibility talimatlarının yalnızca stil rehberine bağlanan bölümleri gerekiyorsa güncellenecek. Bağımsız pipeline repo dosyaları değiştirilmeyecek.

### 6. Teslimatı davranış üzerinden doğrula

- Her aktif stil kuralının kaynağı/kararı ve kapsamı bulunuyor.
- Aynı kaynaktan üretilen AI rehberi, kısa brief ve kullanıcı raporu tutarlı.
- Bir görselin kullanım amacı yalnızca paletse şekil kurallarını değiştirmiyor.
- Çelişkili kaynaklar çözülmüş gibi gösterilmiyor; kullanıcı kararı izlenebiliyor.
- Sıradan model onayı hedef paleti veya diğer kuralları otomatik genişletmiyor.
- Eski/onaysız asset yeni onaylı örnek olarak kullanılamıyor.
- Metin düzeltmesi sıfır model build'i, sıfır render ve sıfır Unity çalıştırmasıyla tamamlanıyor.
- Tek malzeme geri bildirimi alakasız sınıfları yeniden incelemeye göndermiyor.
- Kullanıcı raporundaki montaj/siluet örnekleri doğru; yaklaşık render'lar etiketli.
- Rapor bir kez görsel olarak inceleniyor; yalnızca veri/süreç davranışları için anlamlı testler ekleniyor. Unity/Blender kodu değişmediyse tam oyun testi tekrarlanmıyor.

## Maliyet disiplini

| Durum | AI'ın okuyacağı | Yeniden yapılacak iş |
| --- | --- | --- |
| İlk stil kurulumu | Seçilmiş referanslar ve önemli kırpıntılar | Bir bütüncül görsel yorum; kural ve rapor taslağı |
| Yeni model | Kısa ortak özet, sınıf brief'i, ilgili örnekler | Normal model incelemesi içinde stil kontrolü |
| Küçük geri bildirim | İlgili model/görünüm ve etkilenen kurallar | Sınırlı kural/belge güncellemesi |
| Yeni referans | Yeni görsel ve ilgili mevcut kurallar | İlgili tercihlerin değerlendirilmesi |
| Büyük yön değişikliği | Seçilmiş yeni referans seti | Kapsamı açık yeni stil yorumu |

Yerel görsel hazırlama, isteğe bağlı ölçümler ve rapor üretimi içerik hash'iyle önbelleğe alınacak. Varsayılan akışta her asset için ayrı bir stil agent'ı, yeniden web araştırması veya bütün görselleri tekrar okutma olmayacak. Kapsamlı AI rehberinin boyutu ile günlük üretim bağlamının boyutu birbirinden ayrılacak; brief boyutu ve görüntü sayısı görünür olacak. Kota tüketimi için kesin sayı vaat edilmeyecek.

İlk sürüme otomatik stil sınıflandırıcısı, embedding altyapısı, model eğitimi, otomatik ışık/albedo ayrıştırma veya geniş bir metrik araştırması eklenmeyecek. Bunlar ancak somut bir ihtiyaç kaldığı gösterilirse ayrıca değerlendirilebilir.

## İlk somut teslimat

Bu oyunun mevcut referanslarıyla oluşturulmuş kaynaklı stil taslağı, görsel kullanıcı raporu, ayrıntılı AI rehberi ve üç sınıfa ait kısa brief. Kullanıcının bir örnek düzeltmesinin yalnızca ilgili kurallara ve belgelere yansıdığı gösterilecek. Sonraki gerçek model bu rehberi kullanarak üretilecek; o modelin sonuçları üzerinden stil geliştirmeye devam edilecek.

## Teslim edilen ilk sürüm

- `tools/StyleGuide.py`: kaynak doğrulama, içerik hash'li görsel önbellek, iki belge üretimi, sınıf/malzeme brief'i, değişiklik etkisi, hedefli geri bildirim ve yeni referans kaydı. Yerel çalışır; AI API çağrısı, renderer veya Unity süreci başlatmaz.
- `style.json` schema 3 ve `references.json`: mevcut yedi görüntüden dokuz incelenmiş kırpıntı, 18 taslak kural, emekliye ayrılmış iki eski kural. Eski ölçümler tarihsel veri olarak saklandı; yeni modelleri tanımlayan renk kapısı olmaktan çıkarıldı.
- Üç küçük oyun içi örnek `docs/style/examples/` altında saklandı. Bunlar mevcut üretim örnekleridir; stil onayı değildir. Eski oyun çekimlerinin güncel asset build'iyle birebir eşleştiği yeni render ile doğrulanmadığı için bu sınır kayıtta açıklanır.
- `Builds/Style/art_bible.html`, `style_guide_ai.md` ve `briefs/` aynı kaynaktan üretilir. Kullanıcı raporu kaynak görselleri, sınıf filtrelerini, görsel kuralları, açılır teknik tercihleri ve belirsizlikleri içerir.
- `Style.py`, `Review.py` ve `Verify.ps1` yeni akışa bağlıdır. İnceleme paketinde kısa rehber ve ilgili tek pano bulunur. Tam rehber, tüm referanslar ve eski hero/near-miss panoları otomatik olarak eklenmez.
- Mower/house/tree brief'leri yaklaşık 2.400–2.600 karakter, bir pano ve bir veya iki kırpıntıdır. Metin token tahmini yalnızca UTF-8 bayt / 3 yaklaşımıdır; gerçek kota, görsel, sistem veya geçmiş maliyetini ölçmez.
- Geri bildirim kapsamı, kaynak amacı, eski onay, değişmeyen çıktılar ve build başlatmama davranışları izole testlerle kontrol edilir. Deneme geri bildirimi gerçek stil dosyasına kullanıcı kararı gibi yazılmadı.
- Doğrulama: 74 araç testi başarılı; dört düzenlenen skill şeması geçerli. Rapor tarayıcıda açıldı, kapak ve sınıf filtresi kontrol edildi. Aynı kaynaklarla ikinci belge üretimi çıktı dosyalarını değiştirmedi.
- Oyun kodu, shader'lar, generator'lar ve modeller değiştirilmedi. Bağımsız pipeline reposu kapsam dışında kaldı. Yeni oyun/Blender build'i çalıştırılmadı.

Kullanım başlangıcı: `py -3 tools/Style.py bible`; model işi için `py -3 tools/Style.py brief <model> --class <class>`. Ayrıntılı komutlar README'nin Art style maddesinde ve oyun içindeki art-style skill'inde bulunur.

## İçerik kaybı düzeltmeleri ve kurtarma doğrulaması

24 Ekim 2026, belge revizyonu 2. Kullanıcının son kararı: kaliteyi azaltan token kısıtlarını kaldır; çok oyunlu doğrulamayı sonraya bırak. Önceki sohbet bu düzeltmelerin kodunu, testlerini, skill kopyalarını, README'yi ve üretilen belgeleri kaydetmişti. Kurtarma sırasında mevcut dosyalar sohbet kaydıyla karşılaştırıldı.

- Üretim brief'i ilgili kuralların talimatlarını, kaçınılacakları, gerekçeleri, yüzey bitişi tablosunu ve renk hedeflerini içerir. Karakter veya görsel sayısı yüzünden içerik kesilmez. İlgili panoların yanında tam çözünürlüklü kırpımlar da verilir; önbellek ve kapsam seçimi korunur.
- Geri bildirim eski ayrıntıları silmez. Tam önce/sonra geçmişi tutulur; etkilenen eski ayrıntılar uzlaştırılana kadar işaretlenir. Bekleyen veya geçersizleşmiş kanıt güncel doğrulama sayılmaz.
- Renkler rol bazında, taslak öneriler olarak kontrol edilir. Sayısal taban rengi olmayan kaynak dokular ölçülmemiş kalır. Pah kontrolü artık genel 1–2,5 mm bandını uygulamaz; açık politika kapsamındaki eksik/geçersiz genişlikleri bildirir. Gerçek genişlik parça ölçeği ve görsel incelemeyle seçilir.
- Kullanılmayan schema-2 çalışma yolları ve trailer stil puanlama kodu kaldırıldı. Eski ölçümler ve tarihsel handoff açıklamaları arşiv niteliğinde korunur.
- Güncel brief uzunlukları: mower 12.887, house 12.690, tree 10.164 karakter. Bunlar üst sınır değildir. İlk teslimattaki 2.400–2.600 karakter ve tek pano beklentisi artık geçerli değildir.
- Kurtarma doğrulaması: 69 araç testi geçti; iki stil doğrulayıcısı geçti. Sekiz üretim modeli teknik kontrollerden geçti; kalibrasyon modeli kapsam dışı. `wheel` için 38.080 / 30.000 üçgen bütçesi uyarısı sürüyor. Görsel incelemeler ve kullanıcı onayları tamamlanmış sayılmadı.
- `Style.py bible` tekrar çalıştırıldı: `changed: false`, belge revizyonu 2. Kaynaklar ile rapor/rehber/örnek brief'ler zaten eşitti. Bu kontrol Unity veya Blender çalıştırmadı.
- Rapor tarayıcıda doğrulandı: 36 görsel yüklendi, kırık görsel veya JavaScript hatası yok; masaüstü/mobil yatay taşma yok. Makine filtresi 14 ilgili/ortak kuralı gösterdi, ilgisiz kural göstermedi. Kapak ve renk tablosu görsel olarak incelendi; önizleme yenilendi.
- Çok oyunlu görsel deneme ve bu rehberle yeni modelin uçtan uca görsel doğrulaması henüz yapılmadı. Bunlar ertelenmiş işlerdir; kurtarma kapsamında onaylanmış sonuç gibi sunulmaz.

Sohbetler, kalan işler ve sürüm kontrolü durumu: [24 Ekim devralma kaydı](session-recovery-2026-10-24.md).
