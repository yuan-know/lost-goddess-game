# -*- coding: utf-8 -*-
"""临时工具:去掉字符串/注释后检查 C# 括号配对(沙箱里跑不了 csc,用这个兜底)。"""
import sys


def strip(code):
    out = []
    i = 0
    n = len(code)
    while i < n:
        c = code[i]
        if c == '"':
            i += 1
            while i < n and code[i] != '"':
                if code[i] == '\\':
                    i += 1
                i += 1
            i += 1
            out.append('""')
            continue
        if c == "'":
            i += 1
            while i < n and code[i] != "'":
                if code[i] == '\\':
                    i += 1
                i += 1
            i += 1
            out.append("''")
            continue
        if code.startswith('//', i):
            j = code.find('\n', i)
            i = n if j < 0 else j
            continue
        if code.startswith('/*', i):
            j = code.find('*/', i)
            i = n if j < 0 else j + 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


for f in sys.argv[1:]:
    s = strip(open(f, encoding='utf-8').read())
    ok = (s.count('{') == s.count('}') and s.count('(') == s.count(')')
          and s.count('[') == s.count(']'))
    print(('OK  ' if ok else 'BAD '), f,
          s.count('{'), s.count('}'), s.count('('), s.count(')'), s.count('['), s.count(']'))
