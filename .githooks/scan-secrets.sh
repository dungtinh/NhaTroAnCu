#!/bin/sh
# Đọc một git diff từ stdin, báo lỗi nếu dòng THÊM VÀO có mật khẩu / chuỗi kết nối kèm mật khẩu / file bí mật.
# Dùng chung cho pre-commit và pre-push. Bỏ qua kiểm tra (không khuyến khích): git commit/push --no-verify
awk '
  /^\+\+\+ / { file = substr($0, 7); if (file ~ /(^|\/)(secrets\.json|appsettings\.[^\/]*\.local\.json|Web\.config|[^\/]*\.pubxml\.user|\.env)$/) { print "  File bí mật: " file; bad = 1 } next }
  /^\+/ {
    line = substr($0, 2); low = tolower(line)
    if (low ~ /(^|[^a-z0-9_.])(password|pwd)[[:space:]]*=[[:space:]]*[^;"[:space:]*]/ && low !~ /password[[:space:]]*=[[:space:]]*(\*+|<[^>]*>|\{[^}]*\}|\$)/) { print "  " file ": " line; bad = 1 }
    else if (low ~ /(^|[^a-z0-9_.])(password|pwd)[[:space:]]*=[[:space:]]*"[^"]+"/) { print "  " file ": " line; bad = 1 }
    else if (low ~ /"(password|pwd|apikey|api_key|secret)"[[:space:]]*:[[:space:]]*"[^"]+"/) { print "  " file ": " line; bad = 1 }
    else if (low ~ /-----begin [a-z ]*private key-----/) { print "  " file ": (private key)"; bad = 1 }
  }
  END { exit bad }
'
