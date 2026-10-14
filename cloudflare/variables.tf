variable "account_id" { type = string }
variable "zone_id" { type = string }
variable "zone_name" {
  type = string
  validation {
    condition     = var.zone_name == "collaby.org"
    error_message = "Публичный доступ требует явной настройки зоны collaby.org."
  }
}
variable "tunnel_name" {
  type    = string
  default = "collaby-local"
}
