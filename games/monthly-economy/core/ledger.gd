extends RefCounted
## Experimental double-entry cash ledger. Every posting balances for each owner
## and currency; asset accounts use a debit-positive natural balance.

const MAX_INTEGER: int = 9007199254740991
const BASE_CURRENCY: String = "USD"
const MINOR_UNITS_PER_MAJOR: int = 100
# ISO 4217 currencies verified in the SIX maintenance list (2026-09-17).
const SUPPORTED_CURRENCIES: Array = ["USD", "EUR", "GBP"]
const ACCOUNT_KINDS: Array = ["cash", "equity", "expense", "revenue", "asset", "liability"]

static func supported_currency(code: String) -> bool:
	return code in SUPPORTED_CURRENCIES

static func _integer(value: Variant) -> bool:
	if typeof(value) == TYPE_INT:
		return value >= -MAX_INTEGER and value <= MAX_INTEGER
	if typeof(value) == TYPE_FLOAT:
		return is_finite(value) and value == floor(value) and abs(value) <= MAX_INTEGER
	return false

static func _fail(message: String) -> Dictionary:
	return {"ok": false, "error": message}

static func open_account(s: Dictionary, id: String, owner: String, kind: String, currency: String = "USD") -> Dictionary:
	if id.is_empty() or owner.is_empty() or currency.is_empty() or kind not in ACCOUNT_KINDS:
		return _fail("INVALID_ACCOUNT")
	if not supported_currency(currency):
		return _fail("UNSUPPORTED_CURRENCY")
	if not s.has("accounts"):
		s["accounts"] = {}
	if not s.accounts is Dictionary:
		return _fail("INVALID_ACCOUNTS")
	if s.accounts.has(id):
		if not s.accounts[id] is Dictionary:
			return _fail("INVALID_ACCOUNT")
		var existing: Dictionary = s.accounts[id]
		if existing.get("owner") != owner or existing.get("kind") != kind or existing.get("currency") != currency:
			return _fail("ACCOUNT_ID_COLLISION")
		return {"ok": true, "error": ""}
	s.accounts[id] = {"owner": owner, "kind": kind, "currency": currency, "balance": 0}
	return {"ok": true, "error": ""}

static func balance(s: Dictionary, account: String) -> int:
	if not s.get("accounts", {}) is Dictionary or not s.get("accounts", {}).has(account):
		return 0
	if not s.accounts[account] is Dictionary or not _integer(s.accounts[account].get("balance")):
		return 0
	return int(s.accounts[account].get("balance", 0))

static func post(s: Dictionary, id: String, lines: Array, reason: String, month: int = 0) -> Dictionary:
	if id.is_empty() or reason.is_empty() or month < 0 or lines.size() < 2:
		return _fail("INVALID_JOURNAL")
	if not s.get("accounts", {}) is Dictionary or not s.get("journal", []) is Array:
		return _fail("INVALID_LEDGER")
	# The index is a derived cache, reconciled against every journal row by
	# validate() at turn boundaries and save/load. Legacy index-free ledgers
	# are checked once and receive an index only after a successful posting.
	if s.has("journal_index"):
		if not s.journal_index is Dictionary or s.journal_index.size() != s.get("journal", []).size():
			return _fail("JOURNAL_INDEX_MISMATCH")
		if s.journal_index.has(id):
			return _fail("DUPLICATE_JOURNAL_ID")
	else:
		for existing in s.get("journal", []):
			if not existing is Dictionary:
				return _fail("INVALID_JOURNAL")
			if existing.get("id") == id:
				return _fail("DUPLICATE_JOURNAL_ID")
	var deltas: Dictionary = {}
	var groups: Dictionary = {}
	var normalized: Array = []
	var currencies: Dictionary = {}
	for raw in lines:
		if not raw is Dictionary or not raw.has("account") or not raw.has("debit") or not raw.has("credit"):
			return _fail("INVALID_JOURNAL_LINE")
		var account: String = str(raw.account)
		if not s.get("accounts", {}).has(account):
			return _fail("UNKNOWN_ACCOUNT: " + account)
		if not s.accounts[account] is Dictionary:
			return _fail("INVALID_ACCOUNT: " + account)
		if not _integer(raw.debit) or not _integer(raw.credit):
			return _fail("UNSAFE_MONEY_INTEGER")
		var debit: int = int(raw.debit)
		var credit: int = int(raw.credit)
		if debit < 0 or credit < 0 or (debit == 0 and credit == 0) or (debit > 0 and credit > 0):
			return _fail("INVALID_JOURNAL_AMOUNT")
		var info: Dictionary = s.accounts[account]
		if not info.has("owner") or not info.has("currency") or info.get("kind") not in ACCOUNT_KINDS or not _integer(info.get("balance")):
			return _fail("INVALID_ACCOUNT: " + account)
		if not supported_currency(str(info.currency)):
			return _fail("UNSUPPORTED_CURRENCY")
		var group: String = str(info.owner) + "\u001f" + str(info.currency)
		var amount: int = debit - credit
		groups[group] = int(groups.get(group, 0)) + amount
		deltas[account] = int(deltas.get(account, 0)) + amount
		currencies[str(info.currency)] = true
		if abs(int(groups[group])) > MAX_INTEGER or abs(int(deltas[account])) > MAX_INTEGER:
			return _fail("MONEY_OVERFLOW")
		normalized.append({"account": account, "debit": debit, "credit": credit})
	# Foreign exchange must use an explicit contract and separate currency legs.
	# This P0 journal API intentionally rejects a mixed-currency posting.
	if currencies.size() != 1:
		return _fail("MIXED_CURRENCY_JOURNAL")
	for group in groups:
		if int(groups[group]) != 0:
			return _fail("UNBALANCED_OWNER_CURRENCY")
	for account in deltas:
		var total: int = int(s.accounts[account].balance) + int(deltas[account])
		if abs(total) > MAX_INTEGER:
			return _fail("MONEY_OVERFLOW")
		if s.accounts[account].kind == "cash" and total < 0:
			return _fail("INSUFFICIENT_CASH")
	# The only mutations occur after every line and resulting balance is checked.
	for account in deltas:
		s.accounts[account].balance = int(s.accounts[account].balance) + int(deltas[account])
	if not s.has("journal"):
		s["journal"] = []
	if not s.has("journal_index"):
		s["journal_index"] = {}
		for index in range(s.journal.size()):
			s.journal_index[str(s.journal[index].id)] = index
	s.journal_index[id] = s.journal.size()
	s.journal.append({"id": id, "month": month, "reason": reason, "lines": normalized})
	return {"ok": true, "error": ""}

static func _owner_accounts(s: Dictionary, owner: String, currency: String = "USD") -> Dictionary:
	for kind in ["cash", "equity", "expense", "revenue"]:
		var result: Dictionary = open_account(s, owner + ":" + kind, owner, kind, currency)
		if not result.ok:
			return result
	return {"ok": true, "error": ""}

static func opening(s: Dictionary, owner: String, amount: Variant, month: int = 0) -> Dictionary:
	if owner.is_empty() or not _integer(amount) or amount <= 0 or amount > MAX_INTEGER:
		return _fail("INVALID_OPENING_AMOUNT")
	if not s.get("accounts", {}) is Dictionary or not s.get("journal", []) is Array:
		return _fail("INVALID_LEDGER")
	if s.has("journal_index") and not s.journal_index is Dictionary:
		return _fail("JOURNAL_INDEX_MISMATCH")
	var candidate: Dictionary = {"accounts": s.get("accounts", {}).duplicate(true), "journal": s.get("journal", []).duplicate()}
	if s.has("journal_index"):
		candidate["journal_index"] = s.journal_index.duplicate()
	var result: Dictionary = _owner_accounts(candidate, owner)
	if not result.ok:
		return result
	result = post(candidate, "opening-" + owner, [
		{"account": owner + ":cash", "debit": amount, "credit": 0},
		{"account": owner + ":equity", "debit": 0, "credit": amount}
	], "OPENING_EQUITY", month)
	if result.ok:
		s["accounts"] = candidate.accounts
		s["journal"] = candidate.journal
		s["journal_index"] = candidate.journal_index
	return result

static func transfer(s: Dictionary, id: String, from_owner: String, to_owner: String, amount: Variant, reason: String, month: int = 0) -> Dictionary:
	if from_owner.is_empty() or to_owner.is_empty() or from_owner == to_owner or not _integer(amount) or amount <= 0 or amount > MAX_INTEGER:
		return _fail("INVALID_TRANSFER")
	if not s.get("accounts", {}) is Dictionary or not s.get("journal", []) is Array:
		return _fail("INVALID_LEDGER")
	if s.has("journal_index") and not s.journal_index is Dictionary:
		return _fail("JOURNAL_INDEX_MISMATCH")
	if not s.get("accounts", {}).has(from_owner + ":cash") or not s.get("accounts", {}).has(to_owner + ":cash"):
		return _fail("UNKNOWN_CASH_ACCOUNT")
	if not s.accounts[from_owner + ":cash"] is Dictionary or not s.accounts[to_owner + ":cash"] is Dictionary:
		return _fail("INVALID_CASH_ACCOUNT")
	var from_cash: Dictionary = s.accounts[from_owner + ":cash"]
	var to_cash: Dictionary = s.accounts[to_owner + ":cash"]
	if from_cash.get("owner") != from_owner or to_cash.get("owner") != to_owner or from_cash.get("kind") != "cash" or to_cash.get("kind") != "cash":
		return _fail("CASH_OWNER_MISMATCH")
	if str(from_cash.get("currency", "")).is_empty() or from_cash.get("currency") != to_cash.get("currency"):
		return _fail("CURRENCY_MISMATCH")
	var complete: bool = true
	for owner in [from_owner, to_owner]:
		for kind in ["cash", "equity", "expense", "revenue"]:
			var account_id: String = str(owner) + ":" + str(kind)
			if not s.accounts.has(account_id):
				complete = false
			elif not s.accounts[account_id] is Dictionary or s.accounts[account_id].get("owner") != owner or s.accounts[account_id].get("kind") != kind or s.accounts[account_id].get("currency") != from_cash.currency:
				return _fail("ACCOUNT_ID_COLLISION")
	var lines: Array = [
		{"account": from_owner + ":expense", "debit": amount, "credit": 0},
		{"account": from_owner + ":cash", "debit": 0, "credit": amount},
		{"account": to_owner + ":cash", "debit": amount, "credit": 0},
		{"account": to_owner + ":revenue", "debit": 0, "credit": amount}
	]
	if complete:
		# post() stages every resulting balance before mutating; initialized
		# owners need no ledger copy for an ordinary payment.
		return post(s, id, lines, reason, month)
	var candidate: Dictionary = {"accounts": s.get("accounts", {}).duplicate(true), "journal": s.get("journal", []).duplicate()}
	if s.has("journal_index"):
		candidate["journal_index"] = s.journal_index.duplicate()
	var result: Dictionary = _owner_accounts(candidate, from_owner, str(from_cash.currency))
	if result.ok:
		result = _owner_accounts(candidate, to_owner, str(to_cash.currency))
	if not result.ok:
		return result
	result = post(candidate, id, lines, reason, month)
	if result.ok:
		s["accounts"] = candidate.accounts
		s["journal"] = candidate.journal
		s["journal_index"] = candidate.journal_index
	return result

static func validate(s: Dictionary, allow_legacy_currencies: bool = false) -> Array:
	var errors: Array = []
	if not s.get("accounts") is Dictionary or not s.get("journal") is Array:
		return ["LEDGER_SCHEMA"]
	var reconstructed: Dictionary = {}
	for id in s.accounts:
		var info: Variant = s.accounts[id]
		if not id is String or not info is Dictionary:
			errors.append("ACCOUNT_SCHEMA")
			continue
		if str(info.get("owner", "")).is_empty() or str(info.get("currency", "")).is_empty() or info.get("kind") not in ACCOUNT_KINDS:
			errors.append("ACCOUNT_SCHEMA: " + str(id))
		var code: String=str(info.get("currency", ""))
		if not supported_currency(code) and not (allow_legacy_currencies and code in ["CR","XR","YR"]):
			errors.append("UNSUPPORTED_ACCOUNT_CURRENCY: " + str(id))
		if not _integer(info.get("balance")):
			errors.append("ACCOUNT_UNSAFE_BALANCE: " + str(id))
		elif info.get("kind") == "cash" and int(info.balance) < 0:
			errors.append("NEGATIVE_CASH: " + str(id))
		reconstructed[id] = 0
	var seen: Dictionary = {}
	var row_index: int = 0
	for entry in s.journal:
		if not entry is Dictionary or str(entry.get("id", "")).is_empty() or not entry.get("lines") is Array or entry.get("lines", []).size() < 2:
			errors.append("JOURNAL_SCHEMA")
			continue
		if seen.has(entry.id):
			errors.append("DUPLICATE_JOURNAL_ID: " + str(entry.id))
		seen[entry.id] = row_index
		row_index += 1
		if not _integer(entry.get("month")) or int(entry.get("month", -1)) < 0 or str(entry.get("reason", "")).is_empty():
			errors.append("JOURNAL_METADATA: " + str(entry.id))
		var groups: Dictionary = {}
		var currencies: Dictionary = {}
		for line in entry.lines:
			if not line is Dictionary or not s.accounts.has(line.get("account")) or not _integer(line.get("debit")) or not _integer(line.get("credit")):
				errors.append("JOURNAL_LINE_SCHEMA: " + str(entry.id))
				continue
			if not s.accounts[line.account] is Dictionary:
				errors.append("JOURNAL_ACCOUNT_SCHEMA: " + str(entry.id))
				continue
			var debit: int = int(line.debit)
			var credit: int = int(line.credit)
			if debit < 0 or credit < 0 or (debit == 0 and credit == 0) or (debit > 0 and credit > 0):
				errors.append("JOURNAL_LINE_AMOUNT: " + str(entry.id))
			var info: Dictionary = s.accounts[line.account]
			var group: String = str(info.get("owner")) + "\u001f" + str(info.get("currency"))
			var amount: int = debit - credit
			groups[group] = int(groups.get(group, 0)) + amount
			currencies[str(info.get("currency"))] = true
			reconstructed[line.account] = int(reconstructed.get(line.account, 0)) + amount
			if abs(int(reconstructed[line.account])) > MAX_INTEGER or abs(int(groups[group])) > MAX_INTEGER:
				errors.append("JOURNAL_OVERFLOW: " + str(entry.id))
		if currencies.size() != 1:
			errors.append("MIXED_CURRENCY_JOURNAL: " + str(entry.id))
		for group in groups:
			if int(groups[group]) != 0:
				errors.append("UNBALANCED_OWNER_CURRENCY: " + str(entry.id))
	for id in reconstructed:
		if s.accounts[id] is Dictionary and _integer(s.accounts[id].get("balance")) and int(s.accounts[id].balance) != int(reconstructed[id]):
			errors.append("BALANCE_RECONSTRUCTION: " + str(id))
	if s.has("journal_index"):
		if not s.journal_index is Dictionary or s.journal_index.size() != s.journal.size():
			errors.append("JOURNAL_INDEX_MISMATCH")
		else:
			for id in seen:
				if not s.journal_index.has(id) or not _integer(s.journal_index[id]) or int(s.journal_index[id]) != int(seen[id]):
					errors.append("JOURNAL_INDEX_MISMATCH: " + str(id))
	return errors
