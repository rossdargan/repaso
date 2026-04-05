#!/usr/bin/env python3
import json
import sys
import urllib.request
from pathlib import Path

BASE = 'http://127.0.0.1:5188'


def api_get(path):
    with urllib.request.urlopen(BASE + path, timeout=30) as r:
        return json.loads(r.read().decode('utf-8'))


def api_post(path, payload):
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(BASE + path, data=data, headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode('utf-8'))


def ensure_category(name: str) -> str:
    cat = api_post('/api/categories', {'name': name})
    return cat['id']


def map_gender(value):
    v = (value or '').strip().lower()
    return {'masculine': 1, 'feminine': 2}.get(v, 0)


def map_number(value):
    v = (value or '').strip().lower()
    return {'singular': 1, 'plural': 2}.get(v, 0)


def seed_words(words, category_name):
    category_id = ensure_category(category_name)
    count = 0
    for word in words:
        english = (word.get('english') or '').strip()
        spanish = (word.get('spanish') or '').strip()
        if not english or not spanish:
            continue
        accepted = word.get('acceptedAnswers') or []
        if isinstance(accepted, str):
            accepted = [accepted]
        payload = {
            'englishPrompt': english,
            'additionalEnglishAnswers': [x.strip() for x in accepted if x and x.strip()],
            'spanishPrompt': spanish,
            'additionalSpanishAnswers': [],
            'pronunciation': word.get('pronunciation'),
            'comment': word.get('comment') or word.get('formLabel'),
            'gender': map_gender(word.get('gender')),
            'number': map_number(word.get('number')),
            'state': 0,
            'categoryId': category_id,
        }
        api_post('/api/words', payload)
        count += 1
    return count


def main():
    if len(sys.argv) != 3:
        print('usage: seed_repaso.py <json-file> <category-name>')
        sys.exit(2)
    path = Path(sys.argv[1])
    category_name = sys.argv[2]
    words = json.loads(path.read_text())
    count = seed_words(words, category_name)
    summary = api_get('/api/progress/summary')
    print(json.dumps({'seeded': count, 'totalWords': summary['totalWords']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
