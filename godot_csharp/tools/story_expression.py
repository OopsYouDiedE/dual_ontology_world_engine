"""Validate expression declarations without interpreting story identities or wording."""
from story_base import StoryError


def validate_expression(table, line):
    for group, rows in table.items():
        if not isinstance(rows, dict):
            raise StoryError('Expression group must contain responses: ' + group, line)
        for name, row in rows.items():
            if not isinstance(row, dict) or not isinstance(row.get('text'), str) or not row['text']:
                raise StoryError('Response requires a text key: ' + group + '.' + name, line)
            if 'priority' in row and (isinstance(row['priority'], bool) or not isinstance(row['priority'], int)):
                raise StoryError('Response priority must be an integer: ' + name, line)
            when = row.get('when', {})
            if not isinstance(when, dict) or any(not isinstance(v, (str, int, float, bool)) for v in when.values()):
                raise StoryError('Response conditions must be scalar comparisons: ' + name, line)
            for value in when.values():
                if isinstance(value, str) and value.startswith(('>', '<')):
                    try:
                        float(value.lstrip('><='))
                    except ValueError:
                        raise StoryError('Response ordering comparison requires a number: ' + name, line)
    return table
