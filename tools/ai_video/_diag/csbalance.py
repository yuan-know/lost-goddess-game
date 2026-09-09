# -*- coding: utf-8 -*-
"""粗略检查 C# 文件括号配平。剥离字符串字面量与行注释后再计数。"""
import sys

def strip(line):
    out = []
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        if c == '/' and i + 1 < n and line[i + 1] == '/':
            break                      # 行注释,后面全丢
        if c == '"':                   # 字符串字面量,跳到闭合引号
            i += 1
            while i < n:
                if line[i] == '\\':
                    i += 2
                    continue
                if line[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == "'":                   # 字符字面量
            i += 1
            while i < n:
                if line[i] == '\\':
                    i += 2
                    continue
                if line[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)

p = sys.argv[1]
lines = open(p, encoding='utf-8').read().splitlines()
for name, o, c in (('圆括号', '(', ')'), ('花括号', '{', '}'), ('方括号', '[', ']')):
    depth = 0
    first_neg = None
    for i, ln in enumerate(lines, 1):
        t = strip(ln)
        depth += t.count(o) - t.count(c)
        if depth < 0 and first_neg is None:
            first_neg = (i, ln.strip()[:70])
    status = 'OK' if depth == 0 else '!!! 净深度 %d' % depth
    print('%s: %s' % (name, status))
    if first_neg:
        print('   首个负深度 行%d: %s' % first_neg)
print('行数: %d' % len(lines))
