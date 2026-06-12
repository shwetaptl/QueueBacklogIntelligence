import { useState, useEffect } from 'react'

// ── Default empty form state ───────────────────────────────────────────────
const EMPTY = {
  queueName:         '',
  namespace:         '',
  subscriptionId:    '',
  resourceGroupName: '',
  slaMinutes:        '',
  isEnabled:         true,
  cooldownMinutes:   '',
  warningThreshold:  '',
  criticalThreshold: '',
  teamsWebhookUrl:   '',
  emailRecipients:   '',
}

// ── Validation — returns an object of { fieldName: errorMessage } ──────────
function validate(form) {
  const errors = {}

  if (!form.queueName.trim())
    errors.queueName = 'Required'
  else if (!/^[a-z0-9-]+$/.test(form.queueName))
    errors.queueName = 'Lowercase letters, numbers and hyphens only'

  if (!form.namespace.trim())
    errors.namespace = 'Required'

  if (!form.subscriptionId.trim())
    errors.subscriptionId = 'Required'
  else if (!/^[0-9a-f-]{36}$/i.test(form.subscriptionId.trim()))
    errors.subscriptionId = 'Must be a valid GUID'

  if (!form.resourceGroupName.trim())
    errors.resourceGroupName = 'Required'

  const sla = Number(form.slaMinutes)
  if (!form.slaMinutes)
    errors.slaMinutes = 'Required'
  else if (isNaN(sla) || sla < 1 || sla > 120)
    errors.slaMinutes = 'Must be between 1 and 120'

  const cooldown = Number(form.cooldownMinutes)
  if (!form.cooldownMinutes)
    errors.cooldownMinutes = 'Required'
  else if (isNaN(cooldown) || cooldown < 1 || cooldown > 120)
    errors.cooldownMinutes = 'Must be between 1 and 120'

  const warn = Number(form.warningThreshold)
  if (!form.warningThreshold)
    errors.warningThreshold = 'Required'
  else if (isNaN(warn) || warn <= 0 || warn > 1)
    errors.warningThreshold = 'Must be between 0.1 and 1.0'

  const crit = Number(form.criticalThreshold)
  if (!form.criticalThreshold)
    errors.criticalThreshold = 'Required'
  else if (isNaN(crit) || crit <= 0 || crit > 1)
    errors.criticalThreshold = 'Must be between 0.1 and 1.0'
  else if (!errors.warningThreshold && crit < warn)
    errors.criticalThreshold = 'Must be ≥ Warning threshold'

  if (form.teamsWebhookUrl && !/^https?:\/\/.+/.test(form.teamsWebhookUrl))
    errors.teamsWebhookUrl = 'Must be a valid URL starting with https://'

  return errors
}

// ── Reusable field components ──────────────────────────────────────────────
function Field({ label, error, children }) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
      {children}
      {error && <p className="text-xs text-red-600 mt-0.5">{error}</p>}
    </div>
  )
}

function Input({ name, value, onChange, disabled, placeholder, type = 'text' }) {
  return (
    <input
      type={type}
      name={name}
      value={value}
      onChange={onChange}
      disabled={disabled}
      placeholder={placeholder}
      className={`w-full border rounded-lg px-3 py-1.5 text-sm outline-none
        focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition
        ${disabled ? 'bg-gray-100 text-gray-500 cursor-not-allowed' : 'bg-white'}
      `}
    />
  )
}

// ── QueueForm — exported main component ───────────────────────────────────
// Props:
//   queue     — null (add mode) | queue object (edit mode)
//   onSave    — called with the form data object when submitted
//   onCancel  — called when user cancels
//   saving    — bool — shows spinner while API call is in-flight
export default function QueueForm({ queue, onSave, onCancel, saving }) {
  const isEdit = queue !== null

  // ── Controlled form state ──────────────────────────────────────────────
  // Single state object for all fields — .NET analogy: a ViewModel class
  const [form, setForm] = useState(EMPTY)

  // ── Populate form when editing an existing queue ───────────────────────
  // useEffect with [queue] dependency: re-runs whenever the queue to edit changes
  useEffect(() => {
    if (queue) {
      setForm({
        queueName:         queue.queueName         ?? '',
        namespace:         queue.namespace          ?? '',
        subscriptionId:    queue.subscriptionId     ?? '',
        resourceGroupName: queue.resourceGroupName  ?? '',
        slaMinutes:        String(queue.slaMinutes  ?? 3),
        isEnabled:         queue.isEnabled          ?? true,
        cooldownMinutes:   String(queue.cooldownMinutes ?? 5),
        warningThreshold:  String(queue.warningThreshold ?? 0.6),
        criticalThreshold: String(queue.criticalThreshold ?? 1.0),
        teamsWebhookUrl:   queue.teamsWebhookUrl    ?? '',
        emailRecipients:   queue.emailRecipients    ?? '',
      })
    } else {
      setForm(EMPTY)
    }
  }, [queue])

  // ── Validation errors state ────────────────────────────────────────────
  const [errors, setErrors] = useState({})
  const [touched, setTouched] = useState({})

  // ── Single change handler for ALL inputs ───────────────────────────────
  // e.target.name tells us which field changed (like model binding in .NET MVC)
  function handleChange(e) {
    const { name, value, type, checked } = e.target
    setForm(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }))
    // Mark field as touched so we show its error after the user interacts
    setTouched(prev => ({ ...prev, [name]: true }))
  }

  // ── Submit handler ─────────────────────────────────────────────────────
  function handleSubmit(e) {
    e.preventDefault()  // prevent browser's default form submission (page reload)

    // Mark all fields as touched so all errors show
    const allTouched = Object.keys(form).reduce((acc, k) => ({ ...acc, [k]: true }), {})
    setTouched(allTouched)

    const errs = validate(form)
    setErrors(errs)

    if (Object.keys(errs).length > 0) return  // stop if invalid

    // Convert string numbers back to proper types before sending to API
    onSave({
      queueName:         form.queueName.trim(),
      namespace:         form.namespace.trim(),
      subscriptionId:    form.subscriptionId.trim(),
      resourceGroupName: form.resourceGroupName.trim(),
      slaMinutes:        Number(form.slaMinutes),
      isEnabled:         form.isEnabled,
      cooldownMinutes:   Number(form.cooldownMinutes),
      warningThreshold:  Number(form.warningThreshold),
      criticalThreshold: Number(form.criticalThreshold),
      teamsWebhookUrl:   form.teamsWebhookUrl.trim() || null,
      emailRecipients:   form.emailRecipients.trim() || null,
    })
  }

  // Show error only if field has been touched by the user
  const err = (field) => (touched[field] ? errors[field] : undefined)

  // ── Render ─────────────────────────────────────────────────────────────
  return (
    <form
      onSubmit={handleSubmit}
      className="bg-white border border-blue-100 rounded-xl p-6 shadow-sm mb-4"
    >
      <h2 className="text-sm font-semibold text-gray-700 mb-5">
        {isEdit ? `Edit Queue — ${queue.queueName}` : 'Add New Queue'}
      </h2>

      <div className="grid grid-cols-2 gap-4">

        {/* Queue Name — disabled on edit (it's the primary key) */}
        <Field label="Queue Name *" error={err('queueName')}>
          <Input
            name="queueName"
            value={form.queueName}
            onChange={handleChange}
            disabled={isEdit}
            placeholder="e.g. qbi-queue"
          />
        </Field>

        {/* Namespace */}
        <Field label="Service Bus Namespace *" error={err('namespace')}>
          <Input
            name="namespace"
            value={form.namespace}
            onChange={handleChange}
            disabled={isEdit}
            placeholder="e.g. qbi-sb-ns.servicebus.windows.net"
          />
        </Field>

        {/* Subscription ID */}
        <Field label="Subscription ID *" error={err('subscriptionId')}>
          <Input
            name="subscriptionId"
            value={form.subscriptionId}
            onChange={handleChange}
            disabled={isEdit}
            placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
          />
        </Field>

        {/* Resource Group */}
        <Field label="Resource Group *" error={err('resourceGroupName')}>
          <Input
            name="resourceGroupName"
            value={form.resourceGroupName}
            onChange={handleChange}
            disabled={isEdit}
            placeholder="e.g. qbi-rg"
          />
        </Field>

        {/* SLA Minutes */}
        <Field label="SLA (minutes) *" error={err('slaMinutes')}>
          <Input
            name="slaMinutes"
            value={form.slaMinutes}
            onChange={handleChange}
            type="number"
            placeholder="3"
          />
        </Field>

        {/* Cooldown */}
        <Field label="Cooldown (minutes) *" error={err('cooldownMinutes')}>
          <Input
            name="cooldownMinutes"
            value={form.cooldownMinutes}
            onChange={handleChange}
            type="number"
            placeholder="5"
          />
        </Field>

        {/* Warning Threshold */}
        <Field label="Warning Threshold (0–1) *" error={err('warningThreshold')}>
          <Input
            name="warningThreshold"
            value={form.warningThreshold}
            onChange={handleChange}
            type="number"
            placeholder="0.6"
          />
        </Field>

        {/* Critical Threshold */}
        <Field label="Critical Threshold (0–1) *" error={err('criticalThreshold')}>
          <Input
            name="criticalThreshold"
            value={form.criticalThreshold}
            onChange={handleChange}
            type="number"
            placeholder="1.0"
          />
        </Field>

        {/* Teams Webhook */}
        <Field label="Teams Webhook URL" error={err('teamsWebhookUrl')}>
          <Input
            name="teamsWebhookUrl"
            value={form.teamsWebhookUrl}
            onChange={handleChange}
            placeholder="https://outlook.office.com/webhook/..."
          />
        </Field>

        {/* Email Recipients */}
        <Field label="Email Recipients (comma-separated)">
          <Input
            name="emailRecipients"
            value={form.emailRecipients}
            onChange={handleChange}
            placeholder="team@company.com"
          />
        </Field>

        {/* Is Enabled — checkbox */}
        <div className="flex items-center gap-2 pt-4">
          <input
            type="checkbox"
            id="isEnabled"
            name="isEnabled"
            checked={form.isEnabled}
            onChange={handleChange}
            className="w-4 h-4 rounded border-gray-300 text-blue-600"
          />
          <label htmlFor="isEnabled" className="text-sm text-gray-700">
            Enabled (start monitoring immediately)
          </label>
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex gap-3 mt-6 pt-4 border-t border-gray-100">
        <button
          type="submit"
          disabled={saving}
          className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg
            hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {saving ? 'Saving…' : isEdit ? 'Save Changes' : 'Add Queue'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2 bg-gray-100 text-gray-700 text-sm font-medium rounded-lg
            hover:bg-gray-200 transition-colors"
        >
          Cancel
        </button>
      </div>
    </form>
  )
}
