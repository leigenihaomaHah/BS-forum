export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 32

export function checkPasswordRules(password) {
  const p = password || ''
  return {
    len: p.length >= PASSWORD_MIN && p.length <= PASSWORD_MAX,
    letter: /[A-Za-z]/.test(p),
    digit: /\d/.test(p),
    noSpace: p.length > 0 && !/\s/.test(p),
  }
}

export function passwordError(password) {
  const r = checkPasswordRules(password)
  if (!r.len) return `密码长度需为 ${PASSWORD_MIN}-${PASSWORD_MAX} 位`
  if (!r.noSpace) return '密码不能包含空格'
  if (!r.letter) return '密码需包含字母'
  if (!r.digit) return '密码需包含数字'
  return null
}
