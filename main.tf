terraform {
  required_providers {
    yandex = {
      source = "yandex-cloud/yandex"
    }
    telegram = {
      source  = "yi-jiayu/telegram"
      version = "0.3.1"
    }
  }
  required_version = ">= 0.13"
}

provider "yandex" {
  cloud_id                 = var.cloud_id
  folder_id                = var.folder_id
  service_account_key_file = pathexpand("~/.yc-keys/key.json")
}

provider "telegram" {
  bot_token = var.tg_bot_key
}

resource "yandex_iam_service_account" "sa" {
  name = "vvot31-sa"
}

resource "yandex_resourcemanager_folder_iam_binding" "storage_viewer" {
  folder_id = var.folder_id
  role      = "storage.viewer"
  members = [
    "serviceAccount:${yandex_iam_service_account.sa.id}"
  ]
}

resource "yandex_resourcemanager_folder_iam_binding" "storage_uploader" {
  folder_id = var.folder_id
  role      = "storage.uploader"
  members = [
    "serviceAccount:${yandex_iam_service_account.sa.id}"
  ]
}

resource "yandex_resourcemanager_folder_iam_binding" "editor" {
  folder_id = var.folder_id
  role      = "editor"
  members = [
    "serviceAccount:${yandex_iam_service_account.sa.id}"
  ]
}

resource "yandex_resourcemanager_folder_iam_binding" "function_invoker" {
  folder_id = var.folder_id
  role      = "functions.functionInvoker"
  members = [
    "serviceAccount:${yandex_iam_service_account.sa.id}"
  ]
}

resource "yandex_storage_bucket" "photos" {
  bucket = "vvot31-photos"
}

resource "yandex_function_trigger" "upload_photo" {
  name = "vvot31-photo"
  function {
    id                 = yandex_function.face_detection.id
    service_account_id = yandex_iam_service_account.sa.id
  }
  object_storage {
    bucket_id    = yandex_storage_bucket.photos.id
    create       = true
    batch_cutoff = 0
    batch_size   = "1"
  }
}

data "archive_file" "face_detection_archive" {
  type        = "zip"
  output_path = "face_detection.zip"
  source_dir  = "face-detection-function"
}

resource "yandex_function" "face_detection" {
  name              = "vvot31-face-detection"
  user_hash         = data.archive_file.face_detection_archive.output_md5
  runtime           = "python312"
  memory            = 256
  entrypoint        = "function.handler"
  execution_timeout = "10"
  environment = {
    TASK_QUEUE            = yandex_message_queue.tasks.id
    AWS_ACCESS_KEY_ID     = var.aws_access_key_id
    AWS_SECRET_ACCESS_KEY = var.aws_secret_access_key
    AWS_DEFAULT_REGION    = var.aws_default_region
  }
  content {
    zip_filename = data.archive_file.face_detection_archive.output_path
  }
}

resource "yandex_function_iam_binding" "face_detection_invoker" {
  function_id = yandex_function.face_detection.id
  role        = "functions.functionInvoker"
  members = [
    "system:allUsers"
  ]
}

resource "yandex_message_queue" "tasks" {
  name       = "vvot31-task"
  access_key = var.aws_access_key_id
  secret_key = var.aws_secret_access_key
}

resource "yandex_function_trigger" "task_queued" {
  name = "vvot31-task"
  function {
    id                 = yandex_function.face_cut.id
    service_account_id = yandex_iam_service_account.sa.id
  }
  message_queue {
    queue_id           = yandex_message_queue.tasks.arn
    service_account_id = yandex_iam_service_account.sa.id
    batch_cutoff       = 0
    batch_size         = "1"
  }
}

data "archive_file" "face_cut_archive" {
  type        = "zip"
  output_path = "face_cut.zip"
  source_dir  = "face-cut-function"
}

resource "yandex_function" "face_cut" {
  name              = "vvot31-face-cut"
  user_hash         = data.archive_file.face_cut_archive.output_md5
  runtime           = "python312"
  memory            = 128
  entrypoint        = "function.handler"
  execution_timeout = "10"
  environment = {
    FACES_BUCKET_NAME     = yandex_storage_bucket.faces.bucket
    AWS_ACCESS_KEY_ID     = var.aws_access_key_id
    AWS_SECRET_ACCESS_KEY = var.aws_secret_access_key
    AWS_DEFAULT_REGION    = var.aws_default_region
  }
  content {
    zip_filename = data.archive_file.face_cut_archive.output_path
  }
}

resource "yandex_function_iam_binding" "face_cut_invoker" {
  function_id = yandex_function.face_cut.id
  role        = "functions.functionInvoker"
  members = [
    "system:allUsers"
  ]
}

data "archive_file" "webhook_archive" {
  type        = "zip"
  output_path = "webhook.zip"
  source_dir  = "Webhook"
  excludes = [
    "**/bin",
    "**/obj"
  ]
}

resource "yandex_function" "webhook" {
  name              = "vvot31-bot"
  user_hash         = data.archive_file.webhook_archive.output_md5
  runtime           = "dotnet8"
  memory            = 128
  entrypoint        = "Webhook.Handler"
  execution_timeout = "10"
  environment = {
    FACES_BUCKET_NAME     = yandex_storage_bucket.faces.bucket
    AWS_ACCESS_KEY_ID     = var.aws_access_key_id
    AWS_SECRET_ACCESS_KEY = var.aws_secret_access_key
    TG_BOT_TOKEN          = var.tg_bot_key
    API_GATEWAY_URL       = "https://${yandex_api_gateway.api_gateway.domain}"
  }
  content {
    zip_filename = data.archive_file.webhook_archive.output_path
  }
}

resource "yandex_function_iam_binding" "webhook_invoker" {
  function_id = yandex_function.webhook.id
  role        = "functions.functionInvoker"
  members = [
    "system:allUsers"
  ]
}

resource "yandex_api_gateway" "api_gateway" {
  name = "vvot31-apigw"
  spec = <<-EOT
    openapi: 3.0.0
    info:
      title: Sample API
      version: 1.0.0
    paths:
      /:
        get:
          parameters:
            - name: face
              in: query
              required: true
              schema:
                type: string
          x-yc-apigateway-integration:
            bucket: vvot31-faces
            type: object_storage
            service_account_id: ${yandex_iam_service_account.sa.id}
            object: '{face}'
      /photos/{photo}:
        get:
          parameters:
            - name: photo
              in: path
              required: true
              schema:
                type: string
          x-yc-apigateway-integration:
            bucket: vvot31-photos
            type: object_storage
            service_account_id: ${yandex_iam_service_account.sa.id}
            object: '{photo}'
  EOT
}

resource "telegram_bot_webhook" "webhook" {
  url = "https://functions.yandexcloud.net/${yandex_function.webhook.id}"
}

resource "yandex_storage_bucket" "faces" {
  bucket = "vvot31-faces"
}

variable "cloud_id" {
  type = string
}

variable "folder_id" {
  type = string
}

variable "tg_bot_key" {
  type      = string
  sensitive = true
}

variable "aws_access_key_id" {
  type      = string
  sensitive = true
}

variable "aws_secret_access_key" {
  type      = string
  sensitive = true
}

variable "aws_default_region" {
  type = string
}
