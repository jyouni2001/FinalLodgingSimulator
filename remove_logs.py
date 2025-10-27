import re
import sys

file_path = sys.argv[1] if len(sys.argv) > 1 else 'Assets/02.Scripts/AI/AIAgent.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Remove all Debug.Log lines (including Debug.LogWarning, Debug.LogError)
content = re.sub(r'^\s*Debug\.(Log|LogWarning|LogError).*?\);?\s*$', '', content, flags=re.MULTILINE)

# Remove emoji characters
content = re.sub(r'[^\x00-\x7F\u3000-\u303F\u3040-\u309F\u30A0-\u30FF\uAC00-\uD7AF]+', '', content)

# Remove excessive blank lines (more than 2 consecutive)
content = re.sub(r'\n{3,}', '\n\n', content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print(f'✓ Removed all Debug.Log statements and emojis from {file_path}')

