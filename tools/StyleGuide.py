"""Local, reference-backed style documents. No model calls, rendering or mesh builds.

The detailed guide and the human report share one source. Daily briefs select rules
by class/material without dropping instructions or supporting reference images. Measurements
are optional evidence; they never rewrite the target style or approvals.
"""
from __future__ import annotations

import argparse
import base64
import copy
import datetime
import hashlib
import html
import io
import json
import math
from pathlib import Path
import re
import shutil

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
STYLE_PATH = Path('tools/blender/style/style.json')
REF_PATH = Path('tools/blender/style/references.json')
OUTPUT = Path('Builds/Style')
ASPECTS = {'shape', 'proportion', 'edges', 'detail', 'colour', 'material', 'context', 'construction', 'budget'}
ORIGINS = {'observation': 'Görsel gözlem', 'interpretation': 'Görsel yorum',
           'user_decision': 'Kullanıcı kararı', 'technical': 'Teknik tercih'}
CLASSES = {'machine': 'Makineler', 'building': 'Yapılar', 'tree': 'Ağaçlar',
           'held_tool': 'El aletleri', 'suspended': 'Asılı objeler', 'prop': 'Diğer objeler', '*': 'Ortak'}


def read(path, default=None):
    path = Path(path)
    return json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else default


def digest(value):
    data = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')
    return hashlib.sha256(data).hexdigest()


def file_hash(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_changed(path, data):
    """Identical outputs retain mtimes: no false cache/build invalidation."""
    path = Path(path)
    if not isinstance(data, bytes):
        data = data.encode('utf-8')
    if path.exists() and path.read_bytes() == data:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.tmp')
    temp.write_bytes(data)
    temp.replace(path)
    return True


def write_json(path, value):
    return write_changed(path, json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def local_path(root, relative):
    root = Path(root).resolve()
    path = (root / relative).resolve()
    if not path.is_relative_to(root):
        raise ValueError(f'Path must stay in project: {relative}')
    return path


def load(root):
    root = Path(root)
    style = read(root / STYLE_PATH, {})
    refs = read(root / REF_PATH, {'schema': 1, 'references': []})
    return style, refs


def selected_rules(style, cls, materials=None):
    materials = set(materials or [])
    return [r for r in style.get('rules', []) if r.get('status') != 'retired'
            and ('*' in r.get('scope', ['*']) or cls in r.get('scope', []))
            and (not materials or not r.get('materials') or materials.intersection(r['materials']))]


def validate_data(style, refs, root=None):
    errors = []
    records = refs.get('references', [])
    by_id = {r.get('id'): r for r in records}
    if len(by_id) != len(records):
        errors.append('duplicate reference id')
    if style.get('schema') != 3 or not isinstance(style.get('guideline'), dict):
        errors.append('schema 3 and guideline are required')
    for ref in records:
        rid = ref.get('id', '?')
        if not re.fullmatch(r'[A-Za-z0-9_.-]+', str(rid)):
            errors.append(f'{rid}: invalid reference id')
        if not ref.get('roles') or not set(ref['roles']) <= ASPECTS:
            errors.append(f'{rid}: invalid reference roles')
        if not ref.get('scope') or not ref.get('observation') or not ref.get('limitations'):
            errors.append(f'{rid}: scope, observation and limitations required')
        if not re.fullmatch(r'[a-f0-9]{64}', str(ref.get('sha256', ''))):
            errors.append(f'{rid}: source hash required')
        crop = ref.get('crop')
        if crop is not None and (len(crop) != 4 or any(not isinstance(v, int) for v in crop)
                                 or min(crop[:2]) < 0 or min(crop[2:]) <= 0):
            errors.append(f'{rid}: invalid crop (x,y,width,height)')
            continue
        if root is not None:
            try:
                path = local_path(root, ref.get('path', ''))
                if not path.is_file():
                    errors.append(f'{rid}: missing source image')
                elif file_hash(path) != ref.get('sha256'):
                    errors.append(f'{rid}: source changed; inspect it before updating the stored hash')
                elif crop:
                    with Image.open(path) as im:
                        if crop[0] + crop[2] > im.width or crop[1] + crop[3] > im.height:
                            errors.append(f'{rid}: crop exceeds image')
            except (ValueError, OSError) as exc:
                errors.append(f'{rid}: {exc}')
    seen = set()
    for rule in style.get('rules', []):
        rid = rule.get('id', '?')
        if rid in seen:
            errors.append(f'duplicate rule id: {rid}')
        seen.add(rid)
        if rule.get('status') == 'retired':
            continue
        if not rule.get('text') or not rule.get('scope') or not rule.get('check'):
            errors.append(f'{rid}: text, scope and check required')
        if rule.get('origin') not in ORIGINS or rule.get('aspect') not in ASPECTS:
            errors.append(f'{rid}: invalid origin or aspect')
        if rule.get('status') not in ('draft', 'active'):
            errors.append(f'{rid}: status must be draft or active')
        if rule.get('confidence') not in ('low', 'medium', 'high'):
            errors.append(f'{rid}: confidence required')
        if rule.get('origin') in ('user_decision', 'technical') and not rule.get('decision'):
            errors.append(f'{rid}: explicit decision/source required')
        if rule.get('status') == 'active' and rule.get('origin') != 'technical' and not rule.get('decision'):
            errors.append(f'{rid}: adopting a visual rule needs a recorded decision')
        if not rule.get('evidence') and not rule.get('decision'):
            errors.append(f'{rid}: evidence or decision required')
        for evidence in rule.get('evidence', []):
            ref = by_id.get(evidence.get('reference'))
            aspect = evidence.get('aspect')
            if ref is None:
                errors.append(f'{rid}: unknown reference {evidence.get("reference")}')
            elif aspect not in ref.get('roles', []):
                errors.append(f'{rid}: reference {ref["id"]} is not intended for {aspect}')
            elif aspect != rule.get('aspect'):
                errors.append(f'{rid}: evidence aspect differs from rule; use a source intended for this aspect')
    for example in style.get('guideline', {}).get('examples', []):
        eid = example.get('id', '?')
        if not all(example.get(k) for k in ('id', 'model', 'asset', 'image', 'sha256', 'scope', 'note')):
            errors.append(f'{eid}: incomplete example snapshot')
        if example.get('status') not in ('baseline', 'approved'):
            errors.append(f'{eid}: example status must be baseline or approved')
        if example.get('status') == 'approved' and not example.get('decision'):
            errors.append(f'{eid}: selecting a style exemplar needs an explicit decision')
    if style.get('colour_roles'):
        import Style
        errors.extend(Style.validate_colour_roles(style))
    return errors


def validate_project(root):
    return validate_data(*load(root), root=root)


def require_valid(root):
    style, refs = load(root)
    errors = validate_data(style, refs, root)
    if errors:
        raise ValueError('; '.join(errors))
    return style, refs


def reference_image(root, ref):
    with Image.open(local_path(root, ref['path'])) as im:
        image = im.convert('RGB')
    if ref.get('crop'):
        x, y, w, h = ref['crop']
        image = image.crop((x, y, x + w, y + h))
    return image


def cached_image(root, ref, side=900, write=True):
    full_size = side is None
    key = digest({'sha': ref['sha256'], 'crop': ref.get('crop'), 'side': side,
                  'format': 'png-full-v1' if full_size else 'jpeg-v1'})
    path = Path(root) / OUTPUT / 'cache' / (key + ('.png' if full_size else '.jpg'))
    if write and not path.exists():
        image = reference_image(root, ref)
        if not full_size:
            image.thumbnail((side, side), Image.Resampling.LANCZOS)
        data = io.BytesIO()
        image.save(data, format='PNG' if full_size else 'JPEG', **({} if full_size else {'quality': 88}))
        write_changed(path, data.getvalue())
    return path


def board(root, references, destination, start_number=1):
    if not references:
        return None
    width, cell, height = 1440, 1440 // min(3, len(references)), 420
    canvas = Image.new('RGB', (width, height * math.ceil(len(references) / 3)), '#eee8dc')
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 22)
    except OSError:
        font = ImageFont.load_default()
    for i, ref in enumerate(references):
        image = Image.open(cached_image(root, ref)).convert('RGB')
        image.thumbnail((cell - 24, height - 60), Image.Resampling.LANCZOS)
        x, y = (i % 3) * cell, (i // 3) * height
        canvas.paste(image, (x + (cell - image.width) // 2, y + 12 + (height - 60 - image.height) // 2))
        draw.text((x + 16, y + height - 36), str(i + start_number), fill='#233c38', font=font)
    data = io.BytesIO()
    canvas.save(data, format='PNG')
    write_changed(destination, data.getvalue())
    return str(destination)


def example_state(root, example):
    """A registered baseline is never silently promoted by later model approval."""
    spec = read(Path(root) / 'Assets/Opus/ModelSpecs' / (example['model'] + '.json'), {})
    try:
        picture = local_path(root, example['image'])
        if not picture.is_file() or file_hash(picture) != example.get('sha256'):
            return 'unavailable'
    except (ValueError, OSError):
        return 'unavailable'
    if spec.get('asset') != example.get('asset'):
        return 'outdated'
    if example.get('status') != 'approved':
        return 'baseline'
    approval = read(Path(root) / 'tools/blender/approvals.json', {}).get(example['model'], {})
    if approval.get('asset') != example.get('asset'):
        return 'outdated'
    # Approval also covers texture thumbnails and unresolved critical features.
    import Review
    previous_root = Review.ROOT
    try:
        Review.ROOT = str(root)
        return 'approved' if Review.state(example['model'])['clean'] else 'outdated'
    finally:
        Review.ROOT = previous_root


def rule_signature(rule, refs, style=None):
    # A title, rationale, report prose or document revision does not affect modelling.
    semantic = {k: rule.get(k) for k in ('id', 'text', 'scope', 'materials', 'aspect', 'instructions',
                                        'avoid', 'status', 'check', 'enforce', 'guidance_review')}
    by_id = {r['id']: r for r in refs['references']}
    semantic['evidence'] = [dict({k: by_id[e['reference']].get(k) for k in ('id', 'sha256', 'crop', 'roles', 'scope')},
                                status=e.get('status', 'supporting')) for e in rule.get('evidence', [])]
    style = style or {}
    parameters = {'finish.real_material': style.get('finish_by_kind'),
                  'colour.role_palette': style.get('colour_roles'),
                  'detail.budgets': style.get('stylisation', {}).get('budgets_tris'),
                  'detail.floor': style.get('stylisation', {}).get('detail_floor_m'),
                  'edges.chamfer': style.get('stylisation', {}).get('bevel_policy')}
    if rule['id'] in parameters:
        semantic['parameters'] = parameters[rule['id']]
    return digest(semantic)


def snapshot_data(style, refs):
    return {'schema': 1, 'revision': style['guideline']['revision'], 'classes': style.get('classes', {}),
            'rules': {r['id']: {'hash': rule_signature(r, refs, style), 'scope': r.get('scope', ['*']),
                                'materials': r.get('materials', [])}
                      for r in style['rules'] if r.get('status') != 'retired'}}


def impact_data(before, after):
    changed = sorted(k for k in before['rules'].keys() | after['rules'].keys()
                     if before['rules'].get(k) != after['rules'].get(k))
    scope = sorted({c for k in changed for s in (before, after) for c in s['rules'].get(k, {}).get('scope', [])})
    classes = {**before.get('classes', {}), **after.get('classes', {})}
    affected_classes = sorted(classes) if '*' in scope else scope
    return {'changed_rules': changed, 'affected_classes': affected_classes,
            'affected_models': sorted({m for c in affected_classes for m in classes.get(c, [])}),
            'action': 're-evaluate affected rules; rebuild only if a model change is needed' if changed else 'documents only',
            'automatic_builds': 0}


def text_stats(text, crops=0):
    size = len(text.encode('utf-8'))
    return {'characters': len(text), 'utf8_bytes': size, 'estimated_text_tokens': math.ceil(size / 3),
            'estimate_method': 'UTF-8 bytes / 3, approximate; not account usage or a tokenizer count',
            'image_count': int(crops > 0), 'reference_crops': crops,
            'excluded': 'image tokens, system instructions, chat history and tool results'}


def guidance_notice(rule):
    pending = rule.get('guidance_review', {})
    if pending.get('status') != 'needs_review':
        return ''
    return ('Son kullanıcı kararı önceliklidir. Önceki ayrıntılar kaybolmaması için korunmuştur; '
            'kararla çelişen bir talimatı uygulama. Uygulamadan önce şu alanları kararla uzlaştır: '
            + ', '.join(pending.get('fields', [])) + '.')


def evidence_label(evidence):
    return {'supporting': 'destekleyici kaynak', 'needs_review': 'önceki kaynak; yeni kararı desteklediği henüz doğrulanmadı',
            'superseded': 'yerine yeni karar geçti; uygulama kaynağı değil'}.get(evidence.get('status', 'supporting'), 'kaynak')


def colour_targets(style, name=None, cls=None, materials=None):
    return [r for r in style.get('colour_roles', []) if r.get('status') != 'retired'
            and ((name is None and cls is None) or '*' in r.get('scope', ['*']) or cls in r.get('scope', []))
            and (name is None or not r.get('models') or name in r['models'])
            and (not materials or not r.get('materials') or set(materials).intersection(r['materials']))]


def material_instructions(style, name=None, cls=None, materials=None):
    lines = ['## Yüzey bitişleri',
             'Malzemenin rolüne göre aşağıdaki bitişlerden seçim yap; belirli referans daha dar bir seçim tarif ediyorsa onu uygula.',
             'matte = mat; satin = hafif, geniş yansıma; glossy = belirgin parlak yansıma; metallic = açıkta metal.',
             '| Malzeme | Kullanılabilir bitiş |', '|---|---|']
    lines += [f'| {kind} | {", ".join(finishes)} |' for kind, finishes in style.get('finish_by_kind', {}).items()
              if not materials or kind in materials]
    targets = colour_targets(style, name, cls, materials)
    if targets:
        lines += ['', '## Rol bazlı renk hedefleri',
                  'Bunlar üretim için seçilmiş taslak başlangıç renkleridir; ışıklı görüntüden çıkarılmış kesin albedo değerleri değildir.',
                  '| Rol | Kapsam / malzeme | Renk seçenekleri | Önerilen tolerans |', '|---|---|---|---|']
        lines += [f'| {r["id"]} | {", ".join(r.get("materials", []))}; '
                  f'{", ".join(r.get("models", r.get("scope", [])))}; {", ".join(r.get("note_contains", []))} | '
                  f'{", ".join(r["palette"])} | ΔE76 ≤ {r["tolerance_delta_e"]} ({r["status"]}) |' for r in targets]
        lines += [f'- {r["id"]}: {r["rationale"]}' for r in targets]
        lines += ['Malzeme rengi bu başlangıç hedefinden uzaksa nedeni incele. Sayısal renk verisi olmayan kaynak dokularını '
                  'aynı rol dağılımına göre görsel olarak değerlendir; ölçülmeyen yüzeye renk kontrolünden geçti deme.']
    return lines


def brief(root, name, cls=None, materials=None, write=True, *, allow_unclassified=False):
    root = Path(root)
    if not re.fullmatch(r'[A-Za-z0-9_-]+', name):
        raise ValueError('Use a simple model name (letters, numbers, underscore, hyphen)')
    style, refs = require_valid(root)
    cls = cls or next((c for c, names in style.get('classes', {}).items() if name in names), None)
    choices = ', '.join(sorted(set(style.get('classes', {})) | {'prop'}))
    if cls is None and not allow_unclassified:
        raise ValueError(f'No style class for {name!r}. Specify --class CLASS (choices: {choices}), '
                         'or register the model in style.json classes.')
    if cls is not None and cls not in set(style.get('classes', {})) | {'prop'}:
        raise ValueError(f'Unknown class: {cls}. Choices: {choices}')
    rules = selected_rules(style, cls, materials)
    scores = {}
    for r in rules:
        for e in r.get('evidence', []):
            if e.get('status') == 'superseded':
                continue
            scores[e['reference']] = scores.get(e['reference'], 0) + (2 if '*' not in r['scope'] else 1)
    eligible = {r['id']: r for r in refs['references'] if '*' in r['scope'] or cls in r['scope']}
    chosen = [eligible[rid] for rid in sorted(scores, key=lambda rid: (-scores[rid], rid)) if rid in eligible]
    hashes = {r['id']: rule_signature(r, refs, style) for r in rules}
    lines = [f'# {name} — {CLASSES.get(cls, cls) if cls else "Sınıfsız: ortak kurallar"}', '',
             f'Stil: {style["name"]} | belge r{style["guideline"]["revision"]} | {style["guideline"]["status"]}',
             style['summary'], '', 'Görsel kurallar taslaksa kullanıcı onayı varsayma. Teknik geçiş görsel stil onayı değildir.',
             'Tam rehberi yeniden okuma; ihtiyaç olduğunda ilgili kural kimliğinin ayrıntısını aç.']
    if cls is None:
        lines += ['', 'Stil sınıfı belirlenmedi; yalnızca ortak kurallar dahil edildi. Sınıfa özel stil değerlendirmesi eksik. '
                  f'`py -3 tools/Review.py pack {name} --class CLASS` ile sınıfı seç veya style.json classes kaydını güncelle. '
                  f'Seçenekler: {choices}.']
    lines += ['', '## Uygulanacak kurallar']
    for rule in rules:
        lines += ['', f'### [{rule["id"]}] {rule.get("title", rule["id"])}', rule['text'],
                  f'Durum: {rule["status"]} · {ORIGINS[rule["origin"]]}']
        if guidance_notice(rule):
            lines.append(guidance_notice(rule))
        lines += ['- ' + step for step in rule.get('instructions', []) if step != rule['text']]
        if rule.get('avoid'):
            lines.append('Kaçın: ' + '; '.join(rule['avoid']))
        if rule.get('rationale'):
            lines.append('Gerekçe: ' + rule['rationale'])
        lines += [f'Kaynak: {e["reference"]} — {evidence_label(e)}. {e.get("note", "")}'
                  for e in rule.get('evidence', [])]
        lines.append('Kontrol: ' + rule['check'])
    lines += ['', *material_instructions(style, name, cls, materials)]
    budget_values = style.get('stylisation', {}).get('budgets_tris', {}).get(cls)
    if budget_values:
        lines += ['', f'Teknik başlangıç bütçesi ({cls}): {budget_values[0]:,}–{budget_values[1]:,} üçgen. '
                  'Devralınmış öneridir; alt değer doldurulacak hedef, aralık da görsel stil kanıtı değildir.']
    if chosen:
        lines += ['', '## Referans görselleri',
                  'Panolar genel bakış içindir. Küçük ayrıntıları numarası eşleşen tam çözünürlüklü kırpımdan incele.']
        lines += [f'{i + 1}. {ref["id"]}: {ref["label"]}. {ref["observation"]} Sınır: {ref["limitations"]} '
                  f'[Tam kırpım](<{cached_image(root, ref, side=None, write=False).as_posix()}>)'
                  for i, ref in enumerate(chosen)]
        lines += ['Ekran renkleri ışık ve malzemeyi birlikte içerir; albedo olarak kopyalama.']
    approved = [e for e in style['guideline'].get('examples', [])
                if cls in e.get('scope', []) and example_state(root, e) == 'approved']
    if approved:
        lines += ['', *('Onaylı uygulama örneği: ' + e['image'] for e in approved)]
    elif cls is not None:
        lines += ['', 'Bu sınıf için güncel onaylı uygulama örneği yok; mevcut modeller hedef standardı sayılmaz.']
    lines += ['', 'İnceleme: siluet/oran → yüzey ve malzeme → detay yoğunluğu. Bulguyu kural ve görünümle belirt.',
              'Geri bildirim yalnızca ilgili kuralı değiştirir; genel stil değişikliği olarak varsayma.']
    text = '\n'.join(lines) + '\n'
    path = root / OUTPUT / 'briefs' / (name + '.md')
    boards = [root / OUTPUT / 'briefs' / (name + '_references' + (f'_{i // 3 + 1}' if i else '') + '.png')
              for i in range(0, len(chosen), 3)]
    images = [{'reference': ref['id'], 'number': i + 1,
               'path': str(cached_image(root, ref, side=None, write=write))} for i, ref in enumerate(chosen)]
    stats = text_stats(text, len(chosen))
    stats.update(image_count=len(boards) + len(images), board_count=len(boards), detail_images=len(images))
    result = {'text': text, 'path': str(path), 'board': str(boards[0]) if boards else None,
              'boards': [str(p) for p in boards], 'reference_images': images, 'stats': stats,
              'class': cls, 'scope_status': 'class_scoped' if cls else 'common_only',
              'rules_hash': digest(hashes), 'rule_ids': list(hashes), 'reference_ids': [r['id'] for r in chosen]}
    if write:
        for i, board_path in enumerate(boards):
            board(root, chosen[i * 3:i * 3 + 3], board_path, start_number=i * 3 + 1)
        write_changed(path, text)
        write_json(path.with_suffix('.json'), {k: v for k, v in result.items() if k != 'text'})
    return result


def detailed_text(style, refs):
    lines = [f'# {style["name"]}', '', f'Durum: {style["guideline"]["status"]}; belge revizyonu: {style["guideline"]["revision"]}.',
             'Bu ayrıntılı kaynak belgedir. Günlük üretimde sınıfa özel kısa brief kullan.', '', style['summary']]
    lines += ['', *style['guideline'].get('overview', [])]
    for r in style['rules']:
        if r.get('status') == 'retired':
            continue
        lines += ['', f'## {r["id"]} — {r.get("title", r["text"])}', r['text'],
                  f'Kapsam: {", ".join(r["scope"])}. Malzemeler: {", ".join(r.get("materials", [])) or "tümü"}.',
                  f'Kaynak türü: {ORIGINS[r["origin"]]}; güven: {r["confidence"]}; durum: {r["status"]}.',
                  r.get('rationale', ''), *('- ' + line for line in r.get('instructions', []))]
        if guidance_notice(r):
            lines.append(guidance_notice(r))
        lines += ['Kaçın: ' + '; '.join(r['avoid'])] if r.get('avoid') else []
        lines += [f'Kaynak: {e["reference"]} ({e["aspect"]}) — {evidence_label(e)}. {e.get("note", "")}'
                  for e in r.get('evidence', [])]
        if r.get('decision'):
            lines.append('Karar/gerekçe: ' + r['decision'])
        lines.append('Kontrol: ' + r['check'])
    lines += ['', *material_instructions(style), '', '## Referans kaydı']
    lines += [f'- {r["id"]}: {r["path"]}; crop={r.get("crop")}; amaç={", ".join(r["roles"])}. '
              f'{r["observation"]} Sınır: {r["limitations"]}' for r in refs['references']]
    lines += ['', '## Belirsizlikler', *('- ' + u for u in style['guideline'].get('uncertainties', [])),
              '', '## Devralınan teknik uygulama değerleri',
              'Bu değerler görüntülerden ölçülmedi. İlgili teknik denetimde başlangıç önerisidir; sanat onayı sağlamaz.',
              json.dumps({'stylisation': style.get('stylisation', {}),
                          'finish_by_kind': style.get('finish_by_kind', {})}, ensure_ascii=False, indent=2),
              '', '## Güncelleme geçmişi']
    lines += [f'- {item.get("date", "")}: {item.get("reason", item.get("note", ""))}'
              for item in style['guideline'].get('history', [])]
    return '\n'.join(lines) + '\n'


def image_uri(path):
    return 'data:image/jpeg;base64,' + base64.b64encode(Path(path).read_bytes()).decode('ascii')


def human_html(root, style, refs, stats):
    esc = html.escape
    by_id = {r['id']: r for r in refs['references']}
    urls = {rid: image_uri(cached_image(root, ref)) for rid, ref in by_id.items()}

    def figure(ref, number=None):
        return (f'<figure><img loading="lazy" src="{urls[ref["id"]]}" alt="{esc(ref["label"])}">'
                f'<figcaption><b>{number or ref["id"]} · {esc(ref["label"])}</b>'
                f'<p>{esc(ref["observation"])}</p><small>{esc(ref["limitations"])}</small></figcaption></figure>')

    navigation = ''.join(f'<button data-filter="{esc(c)}">{esc(label)}</button>' for c, label in
                         [('all', 'Tümü'), ('machine', 'Makineler'), ('building', 'Yapılar'), ('tree', 'Ağaçlar'),
                          ('held_tool', 'El aletleri'), ('suspended', 'Asılı objeler')])
    overview = ''.join(f'<p>{esc(p)}</p>' for p in style['guideline'].get('overview', []))
    cover_refs = []
    for cls in ('building', 'machine', 'tree'):
        ref = next((r for r in refs['references'] if cls in r['scope']), None)
        if ref and ref not in cover_refs:
            cover_refs.append(ref)
    cover = ''.join(f'<img src="{urls[r["id"]]}" alt="{esc(r["label"])}">' for r in cover_refs)
    reference_cards = ''.join(figure(r, i + 1) for i, r in enumerate(refs['references']))
    rule_cards, technical_cards = [], []
    for r in style['rules']:
        if r.get('status') == 'retired':
            continue
        evidence = list(dict.fromkeys(e['reference'] for e in r.get('evidence', [])))
        visuals = ''.join(f'<figure><img loading="lazy" src="{urls[e["reference"]]}" '
                          f'alt="{esc(by_id[e["reference"]]["label"])}"><figcaption>{esc(e["reference"])} · '
                          f'{esc(evidence_label(e))}</figcaption></figure>' for e in r.get('evidence', []))
        steps = ''.join(f'<li>{esc(s)}</li>' for s in r.get('instructions', []))
        avoid = ''.join(f'<li>{esc(s)}</li>' for s in r.get('avoid', []))
        sources = ', '.join(evidence) or r.get('decision', '')
        cards = technical_cards if r['origin'] == 'technical' else rule_cards
        notice = f'<p class="note">{esc(guidance_notice(r))}</p>' if guidance_notice(r) else ''
        decision = f'<p>Güncel karar: {esc(r["decision"])}</p>' if r.get('decision') else ''
        cards.append(f'<article class="rule" data-scope="{esc(" ".join(r["scope"]))}" id="{esc(r["id"])}">'
                          f'<div class="rule-images">{visuals}</div><div class="rule-body">'
                          f'<span class="eyebrow">{esc(ORIGINS[r["origin"]])} · {esc(r["id"])}</span>'
                          f'<h3>{esc(r.get("title", r["id"]))}</h3><p class="instruction">{esc(r["text"])}</p>'
                          f'{notice}{decision}'
                          f'<p>{esc(r.get("rationale", ""))}</p><ul>{steps}</ul>'
                          f'<details><summary>Uygulama sınırları ve kontrol</summary><ul>{avoid}</ul>'
                          f'<p>Kaynak: {esc(sources)}</p><p>Kontrol: {esc(r["check"])}</p>'
                          f'<p>Durum: {esc(r["status"])} · Güven: {esc(r["confidence"])}</p></details></div></article>')
    examples = []
    for example in style['guideline'].get('examples', []):
        state = example_state(root, example)
        labels = {'baseline': 'Mevcut üretim · stil onayı yok', 'approved': 'Güncel onaylı uygulama örneği',
                  'outdated': 'Eski sürüm · güncel standart olarak kullanma', 'unavailable': 'Görsel eksik veya değişmiş'}
        picture = ''
        if state != 'unavailable':
            path = local_path(root, example['image'])
            image = Image.open(path).convert('RGB')
            image.thumbnail((1000, 700), Image.Resampling.LANCZOS)
            data = io.BytesIO()
            image.save(data, format='JPEG', quality=88)
            uri = 'data:image/jpeg;base64,' + base64.b64encode(data.getvalue()).decode('ascii')
            picture = f'<img loading="lazy" src="{uri}" alt="{esc(example["model"])}">'
        examples.append(f'<figure>{picture}<figcaption><span class="eyebrow">{labels[state]}</span>'
                        f'<h3>{esc(example["model"])}</h3><p>{esc(example["note"])}</p></figcaption></figure>')
    uncertain = ''.join(f'<li>{esc(s)}</li>' for s in style['guideline'].get('uncertainties', []))
    history = ''.join(f'<li>{esc(str(i.get("date", "")))} — {esc(i.get("reason", i.get("note", "")))}</li>'
                      for i in style['guideline'].get('history', []))
    costs = ''.join(f'<tr><td>{esc(name)}</td><td>{s["characters"]:,}</td><td>~{s["estimated_text_tokens"]:,}</td>'
                    f'<td>{s["board_count"]} pano / {s["detail_images"]} tam kırpım</td></tr>' for name, s in stats.items())
    finishes = ''.join(f'<tr><td>{esc(k)}</td><td>{esc(", ".join(v))}</td></tr>'
                       for k, v in style.get('finish_by_kind', {}).items())
    colours = ''.join(f'<tr><td>{esc(r["id"])}</td><td>'
                      + ''.join(f'<span style="display:inline-block;background:{c};width:22px;height:22px;vertical-align:middle;'
                                f'border:1px solid #888" title="{c}"></span> {c} ' for c in r['palette'])
                      + f'</td><td>ΔE76 ≤ {r["tolerance_delta_e"]} · {esc(r["status"])}</td>'
                      f'<td>{esc(r["rationale"])}</td></tr>' for r in colour_targets(style))
    return f'''<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{esc(style['name'])} · Stil rehberi</title><style>
:root{{color-scheme:light;--ink:#243a37;--muted:#606c66;--paper:#f4f0e7;--line:#d9d9ca;--accent:#a34f35}}
*{{box-sizing:border-box}}body{{margin:0;background:var(--paper);color:var(--ink);font:16px/1.65 system-ui,sans-serif}}
header{{background:#213d38;color:#f8f3e5;padding:44px max(24px,calc((100% - 1180px)/2)) 38px}}
.cover-layout{{display:grid;grid-template-columns:1.2fr 1fr;gap:44px;align-items:center}}.cover-images{{display:grid;grid-template-columns:1fr 1fr;gap:12px}}.cover-images img{{width:100%;height:150px;object-fit:cover;border-radius:10px}}.cover-images img:first-child{{grid-column:span 2;height:205px}}.cover-note{{font-size:12px;opacity:.8;margin-top:10px}}
header p{{max-width:850px}}h1{{font:clamp(32px,4vw,50px)/1.1 Georgia,serif;letter-spacing:-.03em;margin:16px 0 22px}}
h2{{font:34px/1.2 Georgia,serif;margin:0 0 18px}}h3{{font-size:22px;line-height:1.3;margin:12px 0}}
main{{max-width:1230px;margin:auto;padding:36px 24px 80px}}section{{margin-bottom:60px}}p{{margin:10px 0 16px}}
.eyebrow{{text-transform:uppercase;letter-spacing:.1em;font-size:11px;font-weight:750}}.tag{{display:inline-block;padding:5px 12px;border:1px solid #9fbaaa;border-radius:99px;font-size:12px}}
.lede{{font-size:21px;line-height:1.6}}.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(270px,1fr));gap:22px}}
figure{{margin:0;background:#fffdf8;border:1px solid var(--line);border-radius:14px;overflow:hidden}}figure img{{display:block;width:100%;height:245px;object-fit:contain;background:#e5e5d9}}figcaption{{padding:20px}}figcaption p{{font-size:14px}}small{{color:var(--muted);font-size:12px}}
.rule{{background:#fffdf8;border:1px solid var(--line);border-radius:14px;margin:22px 0;overflow:hidden;display:grid;grid-template-columns:34% 66%}}
.rule-images{{background:#e3e6db;display:flex;flex-direction:column;justify-content:center;gap:14px;padding:18px}}.rule-images:empty{{display:none}}.rule:has(.rule-images:empty){{display:block}}
.rule-images img{{width:100%;max-height:240px;object-fit:contain}}.rule-body{{padding:28px 34px}}.instruction{{font-size:19px;font-weight:600}}li{{margin:8px 0}}
details{{border-top:1px solid var(--line);padding-top:12px;font-size:14px}}summary{{cursor:pointer;font-weight:600}}nav{{display:flex;gap:8px;flex-wrap:wrap;position:sticky;top:0;background:var(--paper);padding:12px 0;z-index:1}}
button{{border:1px solid #b5c1b6;background:#fffdf8;color:var(--ink);border-radius:30px;padding:9px 17px;cursor:pointer}}button.active{{background:var(--ink);color:white}}
.note{{padding:24px 30px;border-left:4px solid var(--accent);background:#ebe6d9}}table{{border-collapse:collapse;width:100%;font-size:14px}}td,th{{text-align:left;border-bottom:1px solid var(--line);padding:12px}}.table{{overflow-x:auto}}footer{{color:var(--muted);font-size:13px}}
@media(max-width:700px){{header{{padding:30px 24px}}.cover-layout{{display:block}}.cover-images{{margin-top:24px}}.rule{{display:block}}.rule-body{{padding:22px}}.rule-images{{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr))}}h2{{font-size:28px}}}}
@media print{{nav,button{{display:none}}details{{display:block}}.rule,figure{{break-inside:avoid}}header{{color:#213d38;background:white}}}}
</style></head><body><header><div class="cover-layout"><div><span class="eyebrow">Copycat Opus / 3D asset dili</span><h1>Bir bahçenin<br>ortak görsel dili.</h1>
<span class="tag">{'Taslak' if style['guideline']['status'] == 'draft' else esc(style['guideline']['status'])} · revizyon {style['guideline']['revision']}</span><p class="lede">{esc(style['summary'])}</p></div>
<div><div class="cover-images">{cover}</div><p class="cover-note">Hedef referanslardan: yapı, makine ve ağaç yüzeyi.</p></div></div></header>
<main><section><h2>Nasıl bir görünüm hedefliyoruz?</h2>{overview}</section>
<section><h2>Görsel dayanaklar</h2><p>Her görselin hangi özelliğini kullandığımız kayıtlı. Farklı kaynakların renklerini tek bir zorunlu palete ortalamıyoruz.</p><div class="grid">{reference_cards}</div></section>
<section><h2>Model yaparken uygulanacak kararlar</h2><nav>{navigation}</nav><div id="rules">{''.join(rule_cards)}</div>
<details><summary>Teknik üretim tercihleri · görsellerden ölçülmüş stil kuralları değildir</summary>{''.join(technical_cards)}</details></section>
<section><h2>Malzemeler nasıl görünmeli?</h2><p>Mat yüzeyler yaygın, yumuşak ışık geçişi; saten yüzeyler hafif parlama; parlak yüzeyler daha belirgin yansıma verir. Aynı bitişi bütün malzemelere uygulama.</p>
<div class="table"><table><thead><tr><th>Malzeme</th><th>Kullanılabilir bitiş</th></tr></thead><tbody>{finishes}</tbody></table></div></section>
<section><h2>Renk rolleri</h2><p>Bu renkler mevcut oyunun referansları için önerilen üretim başlangıçlarıdır. Sayısal toleranslar taslak yardımcı kontrollerdir; görsel inceleme ile birlikte kullanılır. Kaynak dokuda sayısal malzeme rengi yoksa kontrol bunu ölçülmedi olarak belirtir.</p>
<div class="table"><table><thead><tr><th>Rol</th><th>Başlangıç renkleri</th><th>Tolerans</th><th>Gerekçe</th></tr></thead><tbody>{colours}</tbody></table></div></section>
<section><h2>Oyundaki mevcut örnekler</h2><p>Bu görüntüler objeleri oyun içindeki montajlarıyla gösterir. Işık ve sis de görüntüye etki eder. Model onayı, bütün özelliklerinin stil standardı olduğu anlamına gelmez.</p><div class="grid">{''.join(examples)}</div></section>
<section class="note"><h2>Henüz kesinleştirmediğimiz noktalar</h2><ul>{uncertain}</ul></section>
<section><h2>Günlük kullanım ve maliyet</h2><p>Model brief'i ilgili kuralların uygulama adımlarını, malzeme tercihlerini ve gerekli görsellerin tamamını içerir. Metin veya görsel sayısı sınırıyla bilgi kesilmez. Önbellek ve kapsam seçimi tekrar işi azaltır. Rapor ve brief üretimi yerel çalışır, AI çağrısı veya Blender/Unity çalıştırması yapmaz.</p>
<div class="table"><table><thead><tr><th>Brief</th><th>Karakter</th><th>Tahmini metin token'ı</th><th>Görseller</th></tr></thead><tbody>{costs}</tbody></table></div>
<small>Tahmin UTF-8 bayt / 3 hesabıdır; gerçek tokenizer veya kota ölçümü değildir. Görsel, sistem talimatı, sohbet geçmişi ve araç çıktısı token'ları hariçtir.</small></section>
<section><h2>Bu sürümde ne değişti?</h2><ul>{history}</ul></section><footer>Kaynak: tools/blender/style/style.json + references.json. Aynı veriden kullanıcı raporu, ayrıntılı AI rehberi ve kısa brief'ler üretilir. Referans görüntülerinin ölçümleri görünümü açıklamaya yardım eder; tek başına stil onayı vermez.</footer></main>
<script>document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{{document.querySelectorAll('[data-filter]').forEach(x=>x.classList.toggle('active',x===b));document.querySelectorAll('.rule').forEach(r=>{{const s=r.dataset.scope.split(' ');r.hidden=!(b.dataset.filter==='all'||s.includes('*')||s.includes(b.dataset.filter));r.style.display=r.hidden?'none':''}})}});document.querySelector('[data-filter="all"]').click();</script></body></html>'''


def build(root):
    root = Path(root)
    style, refs = require_valid(root)
    output = root / OUTPUT
    text = detailed_text(style, refs)
    guide = output / 'style_guide_ai.md'
    changed = write_changed(guide, text)
    stats = {}
    for name, cls in [('mower', 'machine'), ('house', 'building'), ('tree', 'tree')]:
        stats[name] = brief(root, name, cls)['stats']
    report = output / 'art_bible.html'
    changed = write_changed(report, human_html(root, style, refs, stats)) or changed
    # The overview is for a new style session; per-asset review gets a scoped board.
    board(root, refs['references'], output / 'style_board.png')
    manifest = {'schema': 1, 'style_source_hash': digest(style), 'references_hash': digest(refs),
                'guide': text_stats(text), 'briefs': stats, 'revision': style['guideline']['revision'],
                'model_builds': 0, 'ai_calls': 0, 'files': {'guide': str(guide), 'report': str(report)}}
    write_json(output / 'guide_manifest.json', manifest)
    return {'changed': changed, **manifest}


def measure(root, images=None):
    """Per-image evidence only; hashes cache the expensive pixel work, no pooled style."""
    root = Path(root)
    _, references = require_valid(root)
    records = references['references']
    if images:
        records = [{'id': Path(p).stem, 'path': str(local_path(root, p).relative_to(root)),
                    'sha256': file_hash(local_path(root, p)), 'crop': None} for p in images]
    import Style
    results = []
    for ref in records:
        key = digest({'sha': ref['sha256'], 'crop': ref.get('crop'), 'measurement': 'image-evidence-v1'})
        cache = root / OUTPUT / 'cache' / (key + '.json')
        values = read(cache)
        if values is None:
            values = Style.measure(reference_image(root, ref))
            # Bright and dark regions may be different objects: discard misleading lighting proxies.
            for field in ('shadow_shift_a', 'shadow_shift_b', 'shadow_colour', 'light_colour'):
                values.pop(field, None)
            write_json(cache, values)
        results.append({'reference': ref['id'], 'source_hash': ref['sha256'], 'values': values})
    result = {'purpose': 'descriptive image evidence only; not recovered albedo or a style gate',
              'settings': 'CIELAB D65; detail Pillow BICUBIC 256px; image content/framing affects every measure',
              'images': results}
    write_json(root / OUTPUT / 'reference_measurements.json', result)
    return result


def revise(root, rule_id, text, note, patch=None):
    """Apply an actual user decision without discarding unaffected guidance.

    A summary alone cannot reconcile old implementation detail or its evidence.
    A structured patch supplies changed fields and lists unchanged fields the AI
    has reconciled against the decision in reviewed_fields. This is not a claim
    that the user separately approved every retained implementation detail.
    """
    root = Path(root)
    style, refs = require_valid(root)
    rule = next((r for r in style['rules'] if r['id'] == rule_id), None)
    if rule is None or rule.get('status') == 'retired':
        raise ValueError('Unknown or retired rule')
    if not isinstance(note, str) or not note.strip():
        raise ValueError('Actual user feedback is required')
    if patch is not None and not isinstance(patch, dict):
        raise ValueError('Revision patch must be a JSON object')
    updates = copy.deepcopy(patch or {})
    detail_fields = ('instructions', 'avoid', 'rationale', 'evidence')
    allowed = {'text', *detail_fields, 'scope', 'materials', 'reviewed_fields'}
    if set(updates) - allowed:
        raise ValueError('Unknown revision fields: ' + ', '.join(sorted(set(updates) - allowed)))
    if text is not None:
        if 'text' in updates and updates['text'] != text:
            raise ValueError('--text and patch text disagree')
        updates['text'] = text
    if not updates:
        raise ValueError('Provide --text or a nonempty --patch')
    reviewed = updates.pop('reviewed_fields', [])
    if not isinstance(reviewed, list) or any(not isinstance(f, str) or f not in detail_fields for f in reviewed):
        raise ValueError('reviewed_fields must name instructions, avoid, rationale or evidence')
    for field in ('text', 'rationale'):
        if field in updates and (not isinstance(updates[field], str) or (field == 'text' and not updates[field].strip())):
            raise ValueError(f'{field} must be a string' + (' with an instruction' if field == 'text' else ''))
    for field in ('instructions', 'avoid', 'scope', 'materials'):
        if field in updates and (not isinstance(updates[field], list)
                                 or any(not isinstance(item, str) or not item.strip() for item in updates[field])
                                 or (field == 'scope' and not updates[field])):
            raise ValueError(f'{field} must be a list of nonempty strings')
    if 'evidence' in updates:
        if not isinstance(updates['evidence'], list) or any(not isinstance(e, dict) for e in updates['evidence']):
            raise ValueError('evidence must be a list of reference records')
        if any(e.get('status', 'supporting') not in ('supporting', 'needs_review', 'superseded')
               for e in updates['evidence']):
            raise ValueError('Evidence status must be supporting, needs_review or superseded')
    before = snapshot_data(style, refs)
    old = copy.deepcopy(rule)
    checked = set(updates) | set(reviewed)
    pending = [field for field in detail_fields if old.get(field) and field not in checked]
    rule.update(updates)
    rule.update(origin='user_decision', decision=note)
    # Retain the links as visible context. They must not silently become proof of
    # the new decision. Explicitly supplied/reviewed evidence can support it.
    for evidence in rule.get('evidence', []):
        if 'evidence' in pending and evidence.get('status') != 'superseded':
            evidence['status'] = 'needs_review'
        elif 'evidence' in reviewed and evidence.get('status') != 'superseded':
            evidence['status'] = 'supporting'
    if any(e.get('status') == 'needs_review' for e in rule.get('evidence', [])) and 'evidence' not in pending:
        pending.append('evidence')
    rule['guidance_review'] = {
        'status': 'needs_review' if pending else 'reconciled',
        'fields': pending,
        'decision_precedence': 'The latest user decision governs. Reconcile preserved provisional details before use; '
                               'do not follow a retained clause that contradicts it.',
    }
    rule['status'] = 'draft' if pending else 'active'
    guide = style['guideline']
    guide['revision'] += 1
    # Keep full snapshots in an append-only sequence. Repeated edits never
    # overwrite the only copy of an earlier instruction, image link or decision.
    guide.setdefault('history', []).append({'date': datetime.date.today().isoformat(), 'rule': rule_id,
                                          'revision': guide['revision'], 'reason': note,
                                          'before': old['text'], 'after': rule['text'],
                                          'before_rule': old, 'after_rule': copy.deepcopy(rule),
                                          'reconciled_fields': sorted(set(detail_fields) & checked)})
    errors = validate_data(style, refs, root)
    if errors:
        raise ValueError('; '.join(errors))
    write_json(root / STYLE_PATH, style)
    result = impact_data(before, snapshot_data(style, refs))
    write_json(root / OUTPUT / 'last_change.json', result)
    return result


def add_reference(root, source, rid, roles, scope, label='', group='user-reference', crop=None, note=''):
    root = Path(root).resolve()
    source = Path(source).resolve()
    if source.suffix.lower() not in ('.png', '.jpg', '.jpeg', '.webp'):
        raise ValueError('Use PNG, JPEG or WebP')
    if not re.fullmatch(r'[A-Za-z0-9_.-]+', rid) or not roles or not set(roles) <= ASPECTS or not scope:
        raise ValueError('Valid reference id, roles and scope required')
    with Image.open(source) as im:
        im.verify()
    sha = file_hash(source)
    style, refs = load(root)
    if any(r['id'] == rid for r in refs['references']):
        raise ValueError('Reference id already exists; use a new id for changed evidence')
    destination = root / 'docs/style/references' / (sha[:16] + source.suffix.lower())
    if source.is_relative_to(root):
        destination = source
    record = {'id': rid, 'path': destination.relative_to(root).as_posix(), 'sha256': sha, 'crop': crop,
              'roles': roles, 'scope': scope, 'label': label or source.stem, 'group': group,
              'observation': note or 'Yeni kaynak; görsel yorum bekliyor.',
              'limitations': 'Kullanım amacı belirtilmiştir; mevcut hedef kurallarını otomatik değiştirmez.'}
    candidate = copy.deepcopy(refs)
    candidate['references'].append(record)
    errors = validate_data(style, candidate)
    if errors:
        raise ValueError('; '.join(errors))
    if destination != source:
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    errors = validate_data(style, candidate, root)
    if errors:
        raise ValueError('; '.join(errors))
    write_json(root / REF_PATH, candidate)
    return {'reference': rid, 'rules_changed': 0, 'needs_visual_interpretation': True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    sub = parser.add_subparsers(dest='action', required=True)
    for command in ('build', 'validate', 'measure'):
        sub.add_parser(command)
    p = sub.add_parser('brief')
    p.add_argument('name'); p.add_argument('--class', dest='cls'); p.add_argument('--materials')
    p = sub.add_parser('snapshot'); p.add_argument('--out', type=Path, required=True)
    p = sub.add_parser('impact'); p.add_argument('snapshot', type=Path)
    p = sub.add_parser('revise'); p.add_argument('rule'); p.add_argument('--text'); p.add_argument('--note', required=True)
    p.add_argument('--patch', type=Path, help='JSON changed fields and reviewed_fields reconciled against the user note')
    p = sub.add_parser('add-reference'); p.add_argument('path'); p.add_argument('--id', required=True)
    p.add_argument('--roles', required=True); p.add_argument('--scope', required=True); p.add_argument('--crop')
    p.add_argument('--label', default=''); p.add_argument('--group', default='user-reference'); p.add_argument('--note', default='')
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        if args.action == 'validate':
            errors = validate_project(root)
            print('Guideline: ' + ('ok' if not errors else '; '.join(errors)))
            return bool(errors)
        if args.action == 'build':
            result = build(root)
            print(json.dumps({'changed': result['changed'], 'files': result['files'], 'briefs': result['briefs']}, ensure_ascii=False))
        elif args.action == 'brief':
            result = brief(root, args.name, args.cls, args.materials.split(',') if args.materials else None)
            print(json.dumps({k: v for k, v in result.items() if k != 'text'}, ensure_ascii=False))
        elif args.action == 'measure':
            result = measure(root)
            print(f'Measured {len(result["images"])} images; target rules unchanged.')
        elif args.action == 'snapshot':
            write_json(args.out, snapshot_data(*require_valid(root)))
            print(args.out)
        elif args.action == 'impact':
            print(json.dumps(impact_data(read(args.snapshot), snapshot_data(*require_valid(root))), ensure_ascii=False))
        elif args.action == 'revise':
            changes = json.loads(args.patch.read_text(encoding='utf-8-sig')) if args.patch else None
            print(json.dumps(revise(root, args.rule, args.text, args.note, changes), ensure_ascii=False))
        else:
            print(json.dumps(add_reference(root, args.path, args.id, args.roles.split(','), args.scope.split(','),
                                           args.label, args.group, [int(v) for v in args.crop.split(',')] if args.crop else None,
                                           args.note), ensure_ascii=False))
    except (ValueError, OSError, KeyError) as exc:
        parser.exit(1, str(exc) + '\n')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
