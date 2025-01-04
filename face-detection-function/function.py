import json
import boto3.session
import cv2
import cv2.data
import numpy as np

def handler(event, context):
  body = json.loads(event.body)
  bucket_id = body.messages[0].details.bucket_id
  object_id = body.messages[0].details.object_id

  session = boto3.session.Session()
  s3_client = session.client(
    service_name='s3',
    endpoint_url='https://storage.yandexcloud.net'
  )
  q_client = session.client(
    service_name='sqs',
    endpoint_url='https://message-queue.api.cloud.yandex.net'
  )

  get_object_response = s3_client.get_object(Bucket=bucket_id, Key=object_id)
  object = get_object_response['Body'].read()
  object_as_arr = np.frombuffer(object, np.uint8)

  image = cv2.imdecode(object_as_arr, cv2.IMREAD_COLOR)
  gray_image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

  face_classifier = cv2.CascadeClassifier(
    cv2.data.haarcascades + 'haarcascade_frontalface_default.xml'
  )

  faces = face_classifier.detectMultiScale(
    gray_image, scaleFactor=1.1, minNeighbors=5, minSize=(40, 40)
  )

  for (x, y, w, h) in faces:
    payload = json.dumps({
      'object_id': object_id,
      'face': [[x, y], [x+w, y], [x+w, y+h], [x, y+h]]
    })
    q_client.send_message(
      QueueUrl='vvot31-task',
      MessageBody=payload
    )