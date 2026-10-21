"""Launch the repository MCP server from the former adapter command path."""
from pathlib import Path
import runpy
import sys

if __name__ == '__main__':
    tools_root = Path(__file__).resolve().parents[2]
    sys.path.insert(0, str(tools_root))
    runpy.run_path(str(tools_root / 'server.py'), run_name='__main__')
